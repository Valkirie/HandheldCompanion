using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Utils;
using ControllerService.Targets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Device = ControllerCommon.Devices.Device;

namespace ControllerService
{
    public class ControllerService : IHostedService
    {
        // controllers vars
        private ViGEmClient VirtualClient;
        private ViGEmTarget VirtualTarget;

        public XInputController XInputController;

        private PipeServer pipeServer;
        private ProfileManager profileManager;
        private DSUServer DSUServer;
        public HidHide Hidder;

        // Handheld devices vars
        private Device handheldDevice;

        public static string CurrentExe, CurrentPath, CurrentPathDep;
        public static string CurrentTag;
        public static int CurrentOverlayStatus = -1;

        private string DSUip;
        private bool HIDcloaked, HIDuncloakonclose, DSUEnabled;
        private int DSUport, HIDrate;
        private double HIDstrength;

        private HIDmode HIDmode = HIDmode.None;
        private HIDstatus HIDstatus = HIDstatus.Disconnected;

        private readonly ILogger<ControllerService> logger;
        private readonly IHostApplicationLifetime lifetime;

        public ControllerService(ILogger<ControllerService> logger, IHostApplicationLifetime lifetime)
        {
            this.logger = logger;
            this.lifetime = lifetime;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // settings
            HIDcloaked = Properties.Settings.Default.HIDcloaked;
            HIDuncloakonclose = Properties.Settings.Default.HIDuncloakonclose;
            HIDmode = (HIDmode)Properties.Settings.Default.HIDmode;
            HIDstatus = (HIDstatus)Properties.Settings.Default.HIDstatus;
            DSUEnabled = Properties.Settings.Default.DSUEnabled;
            DSUip = Properties.Settings.Default.DSUip;
            DSUport = Properties.Settings.Default.DSUport;
            HIDrate = Properties.Settings.Default.HIDrate;
            HIDstrength = Properties.Settings.Default.HIDstrength;

            // initialize log
            logger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.ProductVersion);

            // verifying ViGEm is installed
            try
            {
                VirtualClient = new ViGEmClient();
            }
            catch (Exception)
            {
                logger.LogCritical("ViGEm is missing. Please get it from: {0}", "https://github.com/ViGEm/ViGEmBus/releases");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            Hidder = new HidHide(logger);
            Hidder.RegisterApplication(CurrentExe);

            // initialize PipeServer
            pipeServer = new PipeServer("ControllerService", logger);
            pipeServer.Connected += OnClientConnected;
            pipeServer.Disconnected += OnClientDisconnected;
            pipeServer.ClientMessage += OnClientMessage;

            // prepare physical controller
            foreach (UserIndex idx in (UserIndex[])Enum.GetValues(typeof(UserIndex)))
            {
                Controller controller = new Controller(idx);
                if (!controller.IsConnected)
                    continue;

                XInputController = new XInputController(controller, idx, logger, pipeServer);
                break;
            }

            if (XInputController == null)
            {
                logger.LogCritical("No physical controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            // XInputController settings
            XInputController.SetVibrationStrength(HIDstrength);
            XInputController.SetPollRate(HIDrate);
            XInputController.Updated += OnTargetSubmited;

            // get the actual handheld device
            var ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            var ProductName = MotherboardInfo.Product;

            switch (ProductName)
            {
                case "AYANEO 2021":
                case "AYANEO 2021 Pro":
                case "AYANEO 2021 Pro Retro Power":
                    handheldDevice = new AYANEO2021(ManufacturerName, ProductName);
                    break;
                case "NEXT Pro":
                case "NEXT Advance":
                case "NEXT":
                    handheldDevice = new AYANEONEXT(ManufacturerName, ProductName);
                    break;
                default:
                    handheldDevice = new DefaultDevice(ManufacturerName, ProductName);
                    logger.LogWarning("{0} from {1} is not yet supported. The behavior of the application will be unpredictable.", ProductName, ManufacturerName);
                    break;
            }
            Hidder.RegisterDevice(XInputController.ControllerIDs);

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport, logger);
            DSUServer.Started += OnDSUStarted;
            DSUServer.Stopped += OnDSUStopped;

            // initialize Profile Manager
            profileManager = new ProfileManager(logger);
            profileManager.Updated += ProfileUpdated;
        }

        private void SetControllerMode(HIDmode mode)
        {
            // disconnect current virtual controller
            VirtualTarget?.Disconnect();

            switch (mode)
            {
                default:
                case HIDmode.None:
                    VirtualTarget = null;
                    break;
                case HIDmode.DualShock4Controller:
                    VirtualTarget = new DualShock4Target(XInputController, VirtualClient, XInputController.physicalController, (int)XInputController.UserIndex, logger);
                    break;
                case HIDmode.Xbox360Controller:
                    VirtualTarget = new Xbox360Target(XInputController, VirtualClient, XInputController.physicalController, (int)XInputController.UserIndex, logger);
                    break;
            }

            if (VirtualTarget == null)
                return;

            VirtualTarget.Connected += OnTargetConnected;
            VirtualTarget.Disconnected += OnTargetDisconnected;

            XInputController.SetViGEmTarget(VirtualTarget);
            SetControllerStatus(HIDstatus);
        }

        private void SetControllerStatus(HIDstatus status)
        {
            HIDstatus = status;
            switch (status)
            {
                case HIDstatus.Connected:
                    VirtualTarget?.Connect();
                    break;
                case HIDstatus.Disconnected:
                    VirtualTarget?.Disconnect();
                    break;
            }
        }

        private void OnTargetDisconnected(ViGEmTarget target)
        {
            // send notification
            pipeServer?.SendMessage(new PipeServerToast
            {
                title = $"{target}",
                content = "Virtual device is now disconnected",
                image = $"HIDmode{(uint)target.HID}"
            });
        }

        private void OnTargetConnected(ViGEmTarget target)
        {
            // send notification
            pipeServer?.SendMessage(new PipeServerToast
            {
                title = $"{target}",
                content = "Virtual device is now connected",
                image = $"HIDmode{(uint)target.HID}"
            });
        }

        private void OnTargetSubmited(XInputController controller)
        {
            DSUServer?.SubmitReport(controller);
        }

        private void OnDSUStopped(DSUServer server)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = false;

            PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
            pipeServer.SendMessage(settings);
        }

        private void OnDSUStarted(DSUServer server)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = true;

            PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
            pipeServer.SendMessage(settings);
        }

        private void OnClientMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.FORCE_SHUTDOWN:
                    Hidder?.SetCloaking(false, XInputController.ProductName);
                    break;

                case PipeCode.CLIENT_PROFILE:
                    PipeClientProfile profile = (PipeClientProfile)message;
                    ProfileUpdated(profile.profile, true);
                    break;

                case PipeCode.CLIENT_CURSOR:
                    PipeClientCursor cursor = (PipeClientCursor)message;

                    switch (cursor.action)
                    {
                        case CursorAction.CursorUp:
                            XInputController.Touch.OnMouseUp(cursor.x, cursor.y, cursor.button, cursor.flags);
                            break;
                        case CursorAction.CursorDown:
                            XInputController.Touch.OnMouseDown(cursor.x, cursor.y, cursor.button, cursor.flags);
                            break;
                        case CursorAction.CursorMove:
                            XInputController.Touch.OnMouseMove(cursor.x, cursor.y, cursor.button, cursor.flags);
                            break;
                    }
                    break;

                case PipeCode.CLIENT_SETTINGS:
                    PipeClientSettings settings = (PipeClientSettings)message;
                    UpdateSettings(settings.settings);
                    break;

                case PipeCode.CLIENT_HIDDER:
                    PipeClientHidder hidder = (PipeClientHidder)message;

                    switch (hidder.action)
                    {
                        case HidderAction.Register:
                            Hidder.RegisterApplication(hidder.path);
                            break;
                        case HidderAction.Unregister:
                            Hidder.UnregisterApplication(hidder.path);
                            break;
                    }
                    break;

                case PipeCode.CLIENT_NAVIGATED:
                    PipeNavigation navigation = (PipeNavigation)message;
                    CurrentTag = navigation.Tag;

                    switch (navigation.Tag)
                    {
                        case "ProfileSettingsMode0":
                            // do something
                            break;
                        case "ProfileSettingsMode1":
                            // do something
                            break;
                        default:
                            break;
                    }

                    break;

                case PipeCode.CLIENT_OVERLAY:
                    PipeOverlay overlay = (PipeOverlay)message;
                    CurrentOverlayStatus = overlay.Visibility;
                    break;
            }
        }

        private void OnClientDisconnected(object sender)
        {
            XInputController.Touch.OnMouseUp(0, 0, CursorButton.TouchLeft, 26);
            XInputController.Touch.OnMouseUp(0, 0, CursorButton.TouchRight, 26);
        }

        private void OnClientConnected(object sender)
        {
            // send controller details
            pipeServer.SendMessage(new PipeServerHandheld()
            {
                ManufacturerName = handheldDevice.ManufacturerName,
                ProductName = handheldDevice.ProductName,
                ProductIllustration = handheldDevice.ProductIllustration,

                SensorName = handheldDevice.sensorName,
                ProductSupported = handheldDevice.ProductSupported,

                hasAccelerometer = handheldDevice.hasAccelerometer,
                hasGyrometer = handheldDevice.hasGyrometer,
                hasInclinometer = handheldDevice.hasInclinometer,

                ControllerName = XInputController.ProductName,
                ControllerVID = XInputController.XInputData.VID,
                ControllerPID = XInputController.XInputData.PID,
                ControllerIdx = (int)XInputController.UserIndex
            });

            // send server settings
            pipeServer.SendMessage(new PipeServerSettings() { settings = GetSettings() });
        }

        internal void ProfileUpdated(Profile profile, bool backgroundtask)
        {
            XInputController.SetProfile(profile);
        }

        public void UpdateSettings(Dictionary<string, object> args)
        {
            foreach (KeyValuePair<string, object> pair in args)
            {
                string name = pair.Key;
                object property = pair.Value;

                SettingsProperty setting = Properties.Settings.Default.Properties[name];
                if (setting != null)
                {
                    object prev_value = Properties.Settings.Default[name];
                    object value = property;

                    TypeCode typeCode = Type.GetTypeCode(setting.PropertyType);
                    switch (typeCode)
                    {
                        case TypeCode.Boolean:
                            value = (bool)value;
                            prev_value = (bool)prev_value;
                            break;
                        case TypeCode.Single:
                        case TypeCode.Decimal:
                            value = (float)value;
                            prev_value = (float)prev_value;
                            break;
                        case TypeCode.Double:
                            value = (double)value;
                            prev_value = (double)prev_value;
                            break;
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                            value = (int)value;
                            prev_value = (int)prev_value;
                            break;
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            value = (uint)value;
                            prev_value = (uint)prev_value;
                            break;
                        default:
                            value = (string)value;
                            prev_value = (string)prev_value;
                            break;
                    }

                    Properties.Settings.Default[name] = value;
                    ApplySetting(name, prev_value, value);

                    logger.LogDebug("{0} set to {1}", name, property.ToString());
                }
            }

            Properties.Settings.Default.Save();
        }

        private void ApplySetting(string name, object prev_value, object value)
        {
            if (prev_value.ToString() != value.ToString())
            {
                switch (name)
                {
                    case "HIDcloaked":
                        Hidder.SetCloaking((bool)value, XInputController.ProductName);
                        HIDcloaked = (bool)value;
                        break;
                    case "HIDuncloakonclose":
                        HIDuncloakonclose = (bool)value;
                        break;
                    case "HIDmode":
                        SetControllerMode((HIDmode)value);
                        break;
                    case "HIDstatus":
                        SetControllerStatus((HIDstatus)value);
                        break;
                    case "HIDrate":
                        XInputController.SetPollRate((int)value);
                        break;
                    case "HIDstrength":
                        XInputController.SetVibrationStrength((double)value);
                        break;
                    case "DSUEnabled":
                        switch ((bool)value)
                        {
                            case true: DSUServer.Start(); break;
                            case false: DSUServer.Stop(); break;
                        }
                        break;
                    case "DSUip":
                        DSUServer.ip = (string)value;
                        break;
                    case "DSUport":
                        DSUServer.port = (int)value;
                        break;
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            lifetime.ApplicationStarted.Register(OnStarted);
            lifetime.ApplicationStopping.Register(OnStopping);
            lifetime.ApplicationStopped.Register(OnStopped);

            // turn on cloaking
            Hidder.SetCloaking(HIDcloaked, XInputController.ProductName);

            // start DSUClient
            if (DSUEnabled) DSUServer.Start();

            // update virtual controller
            SetControllerMode(HIDmode);
            SetControllerStatus(HIDstatus);

            // start Pipe Server
            pipeServer.Start();

            // start and stop Profile Manager
            profileManager.Start("Default.json");
            profileManager.Stop();

            // listen to system events
            SystemEvents.PowerModeChanged += OnPowerChange;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // turn off cloaking
            Hidder?.SetCloaking(!HIDuncloakonclose, XInputController.ProductName);

            // update virtual controller
            SetControllerStatus(HIDstatus.Disconnected);

            // stop listening to system events
            SystemEvents.PowerModeChanged -= OnPowerChange;

            // stop DSUClient
            DSUServer?.Stop();

            // stop Pipe Server
            pipeServer?.Stop();

            return Task.CompletedTask;
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            logger.LogInformation("Device power mode set to {0}", e.Mode);

            switch (e.Mode)
            {
                default:
                case PowerModes.StatusChange:
                    break;
                case PowerModes.Resume:
                    // (re)initialize sensors
                    XInputController?.UpdateSensors();
                    break;
                case PowerModes.Suspend:
                    break;
            }
        }

        private void OnStarted()
        {
            // Perform post-startup activities here
        }

        private void OnStopping()
        {
            // Perform on-stopping activities here
        }

        private void OnStopped()
        {
            // Perform post-stopped activities here
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            foreach (SettingsProperty s in Properties.Settings.Default.Properties)
                settings.Add(s.Name, Properties.Settings.Default[s.Name].ToString());

            return settings;
        }
    }
}
