using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Utils;
using ControllerService.Targets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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

        // devices vars
        private Device handheldDevice;
        private UserIndex HIDidx;
        private string deviceInstancePath;
        private string baseContainerDeviceInstancePath;

        public static string CurrentExe, CurrentPath, CurrentPathDep;
        public static string CurrentTag;
        public static int CurrentOverlayStatus = -1;

        // settings vars
        Configuration configuration;

        private string DSUip;
        private bool HIDcloaked, HIDuncloakonclose, DSUEnabled;
        private int DSUport, HIDrate;
        private double HIDstrength;

        private HIDmode HIDmode = HIDmode.NoController;
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
            // todo: move me to a specific class
            configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            HIDcloaked = bool.Parse(configuration.AppSettings.Settings["HIDcloaked"].Value);
            HIDuncloakonclose = bool.Parse(configuration.AppSettings.Settings["HIDuncloakonclose"].Value); // Properties.Settings.Default.HIDuncloakonclose;
            HIDmode = Enum.Parse<HIDmode>(configuration.AppSettings.Settings["HIDmode"].Value); // Properties.Settings.Default.HIDmode;
            HIDstatus = Enum.Parse<HIDstatus>(configuration.AppSettings.Settings["HIDstatus"].Value); // Properties.Settings.Default.HIDstatus;
            DSUEnabled = bool.Parse(configuration.AppSettings.Settings["DSUEnabled"].Value); // Properties.Settings.Default.DSUEnabled;
            DSUip = configuration.AppSettings.Settings["DSUip"].Value; // Properties.Settings.Default.DSUip;
            DSUport = int.Parse(configuration.AppSettings.Settings["DSUport"].Value); // Properties.Settings.Default.DSUport;
            HIDrate = int.Parse(configuration.AppSettings.Settings["HIDrate"].Value); // Properties.Settings.Default.HIDrate;
            HIDstrength = double.Parse(configuration.AppSettings.Settings["HIDstrength"].Value); // Properties.Settings.Default.HIDstrength;

            HIDidx = Enum.Parse<UserIndex>(configuration.AppSettings.Settings["HIDidx"].Value); // Properties.Settings.Default.HIDidx;
            deviceInstancePath = configuration.AppSettings.Settings["deviceInstancePath"].Value; // Properties.Settings.Default.deviceInstancePath;
            baseContainerDeviceInstancePath = configuration.AppSettings.Settings["baseContainerDeviceInstancePath"].Value; // Properties.Settings.Default.baseContainerDeviceInstancePath;

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

            // XInputController settings
            XInputController = new XInputController(logger, pipeServer);
            XInputController.SetVibrationStrength(HIDstrength);
            XInputController.SetPollRate(HIDrate);
            XInputController.Updated += OnTargetSubmited;

            // prepare physical controller
            SetControllerIdx(HIDidx, deviceInstancePath, baseContainerDeviceInstancePath);

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
            XInputController.SetDevice(handheldDevice);

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport, logger);
            DSUServer.Started += OnDSUStarted;
            DSUServer.Stopped += OnDSUStopped;

            // initialize Profile Manager
            profileManager = new ProfileManager(logger);
            profileManager.Updated += ProfileUpdated;
        }

        private void SetControllerIdx(UserIndex idx, string deviceInstancePath, string baseContainerDeviceInstancePath)
        {
            ControllerEx controller = new ControllerEx(idx, logger);
            XInputController.SetController(controller); // set even if disconnected

            if (!controller.IsConnected())
            {
                logger.LogWarning("No physical controller detected on UserIndex: {0}", idx);
                return;
            }

            logger.LogInformation("Listening to physical controller on UserIndex: {0}", idx);

            // clear previous values
            List<string> deviceInstancePaths = Hidder.GetRegisteredDevices();

            // exclude controller
            deviceInstancePaths.Remove(deviceInstancePath);
            deviceInstancePaths.Remove(baseContainerDeviceInstancePath);

            foreach (string instancePath in deviceInstancePaths)
                Hidder.UnregisterController(instancePath);

            // register controller
            Hidder.RegisterController(deviceInstancePath);
            Hidder.RegisterController(baseContainerDeviceInstancePath);

            // update settings
            configuration.AppSettings.Settings["HIDidx"].Value = ((int)idx).ToString();
            configuration.AppSettings.Settings["deviceInstancePath"].Value = deviceInstancePath;
            configuration.AppSettings.Settings["baseContainerDeviceInstancePath"].Value = baseContainerDeviceInstancePath;
            configuration.Save(ConfigurationSaveMode.Modified);
        }

        private void SetControllerMode(HIDmode mode)
        {
            // disconnect current virtual controller
            VirtualTarget?.Disconnect();

            switch (mode)
            {
                default:
                case HIDmode.NoController:
                    VirtualTarget = null;
                    break;
                case HIDmode.DualShock4Controller:
                    VirtualTarget = new DualShock4Target(XInputController, VirtualClient, logger);
                    break;
                case HIDmode.Xbox360Controller:
                    VirtualTarget = new Xbox360Target(XInputController, VirtualClient, logger);
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
                content = Properties.Resources.ToastOnTargetDisconnected,
                image = $"HIDmode{(uint)target.HID}"
            });
        }

        private void OnTargetConnected(ViGEmTarget target)
        {
            // send notification
            pipeServer?.SendMessage(new PipeServerToast
            {
                title = $"{target}",
                content = Properties.Resources.ToastOnTargetConnected,
                image = $"HIDmode{(uint)target.HID}"
            });
        }

        private void OnTargetSubmited(XInputController controller)
        {
            DSUServer?.SubmitReport(controller);
        }

        // deprecated
        private void OnDSUStopped(DSUServer server)
        {
            /* DSUEnabled = false;
            configuration.GetSection("Settings:DSUEnabled").Value = false.ToString();

            PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
            pipeServer.SendMessage(settings); */
        }

        // deprecated
        private void OnDSUStarted(DSUServer server)
        {
            /* DSUEnabled = true;
            configuration.GetSection("Settings:DSUEnabled").Value = true.ToString();

            PipeServerSettings settings = new PipeServerSettings("DSUEnabled", DSUEnabled.ToString());
            pipeServer.SendMessage(settings); */
        }

        private void OnClientMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.CLIENT_CONTROLLERINDEX:
                    PipeControllerIndex pipeControllerIndex = (PipeControllerIndex)message;
                    SetControllerIdx((UserIndex)pipeControllerIndex.UserIndex, pipeControllerIndex.deviceInstancePath, pipeControllerIndex.baseContainerDeviceInstancePath);
                    break;

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
                ControllerVID = XInputController.controllerEx.GetVID(),
                ControllerPID = XInputController.controllerEx.GetVID(),
                ControllerIdx = (int)XInputController.controllerEx.UserIndex
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
                string property = pair.Value.ToString();

                configuration.AppSettings.Settings[name].Value = property;
                configuration.Save(ConfigurationSaveMode.Modified);

                ApplySetting(name, property);
                logger.LogDebug("{0} set to {1}", name, property);
            }
        }

        private void ApplySetting(string name, string property)
        {
            switch (name)
            {
                case "HIDcloaked":
                    {
                        bool value = bool.Parse(property);
                        Hidder.SetCloaking(value, XInputController.ProductName);
                        HIDcloaked = value;
                    }
                    break;
                case "HIDuncloakonclose":
                    {
                        bool value = bool.Parse(property);
                        HIDuncloakonclose = value;
                    }
                    break;
                case "HIDmode":
                    {
                        HIDmode value = Enum.Parse<HIDmode>(property);
                        SetControllerMode(value);
                    }
                    break;
                case "HIDstatus":
                    {
                        HIDstatus value = Enum.Parse<HIDstatus>(property);
                        SetControllerStatus(value);
                    }
                    break;
                case "HIDrate":
                    {
                        int value = int.Parse(property);
                        XInputController.SetPollRate(value);
                    }
                    break;
                case "HIDstrength":
                    {
                        double value = double.Parse(property);
                        XInputController.SetVibrationStrength(value);
                    }
                    break;
                case "DSUEnabled":
                    {
                        bool value = bool.Parse(property);
                        switch (value)
                        {
                            case true: DSUServer.Start(); break;
                            case false: DSUServer.Stop(); break;
                        }
                    }
                    break;
                case "DSUip":
                    {
                        string value = property;
                        DSUServer.ip = value;
                    }
                    break;
                case "DSUport":
                    {
                        int value = int.Parse(property);
                        DSUServer.port = value;
                    }
                    break;
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

            foreach (string key in configuration.AppSettings.Settings.AllKeys)
                settings.Add(key, configuration.AppSettings.Settings[key].Value);

            return settings;
        }
    }
}
