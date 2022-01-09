using ControllerCommon;
using ControllerService.Targets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
using System.Threading;
using System.Threading.Tasks;

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

        public static string CurrentExe, CurrentPath, CurrentPathCli, CurrentPathDep, CurrentPathProfiles;

        private string DSUip;
        private bool HIDcloaked, HIDuncloakonclose, DSUEnabled;
        private int DSUport, HIDrate, HIDstrength;

        private HIDmode HIDmode;

        private readonly ILogger<ControllerService> logger;

        public ControllerService(ILogger<ControllerService> logger)
        {
            this.logger = logger;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // settings
            HIDcloaked = Properties.Settings.Default.HIDcloaked;
            HIDuncloakonclose = Properties.Settings.Default.HIDuncloakonclose;
            HIDmode = (HIDmode)Properties.Settings.Default.HIDmode;
            DSUEnabled = Properties.Settings.Default.DSUEnabled;
            DSUip = Properties.Settings.Default.DSUip;
            DSUport = Properties.Settings.Default.DSUport;
            HIDrate = Properties.Settings.Default.HIDrate;
            HIDstrength = Properties.Settings.Default.HIDstrength;

            // initialize log
            logger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.ProductVersion);

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathCli))
            {
                logger.LogCritical("HidHide is missing. Please get it from: {0}", "https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

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
            Hidder = new HidHide(CurrentPathCli, logger, this);
            Hidder.RegisterApplication(CurrentExe);

            // prepare physical controller
            DirectInput dinput = new DirectInput();
            IList<DeviceInstance> dinstances = dinput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            foreach (UserIndex idx in (UserIndex[])Enum.GetValues(typeof(UserIndex)))
            {
                Controller controller = new Controller(idx);
                if (!controller.IsConnected)
                    continue;

                XInputController = new XInputController(controller, idx, logger);
                XInputController.Instance = dinstances[(int)idx];
                break;
            }

            if (XInputController == null)
            {
                logger.LogCritical("No physical controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            var Gyrometer = new XInputGirometer(XInputController, logger);
            if (Gyrometer.sensor == null)
                logger.LogWarning("No Gyrometer detected");
            XInputController.SetGyroscope(Gyrometer);

            var Accelerometer = new XInputAccelerometer(XInputController, logger);
            if (Accelerometer.sensor == null)
                logger.LogWarning("No Accelerometer detected");
            XInputController.SetAccelerometer(Accelerometer);

            var Inclinometer = new XInputInclinometer(XInputController, logger);
            if (Inclinometer.sensor == null)
                logger.LogWarning("No Inclinometer detected");
            XInputController.SetInclinometer(Inclinometer);

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport, logger);
            DSUServer.Started += OnDSUStarted;
            DSUServer.Stopped += OnDSUStopped;

            // initialize PipeServer
            pipeServer = new PipeServer("ControllerService", logger);
            pipeServer.Connected += OnClientConnected;
            pipeServer.Disconnected += OnClientDisconnected;
            pipeServer.ClientMessage += OnClientMessage;

            // initialize Profile Manager
            profileManager = new ProfileManager(CurrentPathProfiles, logger);
            profileManager.Updated += ProfileUpdated;
        }

        private void UpdateVirtualController(HIDmode mode)
        {
            VirtualTarget?.Disconnect();

            switch (mode)
            {
                default:
                case HIDmode.DualShock4Controller:
                    VirtualTarget = new DualShock4Target(XInputController, VirtualClient, XInputController.physicalController, (int)XInputController.UserIndex, HIDrate, logger);
                    break;
                case HIDmode.Xbox360Controller:
                    VirtualTarget = new Xbox360Target(XInputController, VirtualClient, XInputController.physicalController, (int)XInputController.UserIndex, HIDrate, logger);
                    break;
            }

            if (VirtualTarget == null)
            {
                logger.LogCritical("Virtual controller initialization failed. Application will stop");
                throw new InvalidOperationException();
            }

            VirtualTarget.Submited += OnTargetSubmited;
            VirtualTarget.Connected += OnTargetConnected;
            // VirtualTarget.Disconnected += OnTargetDisconnected;

            XInputController.SetTarget(VirtualTarget);
            XInputController.virtualTarget?.Connect();
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

        private void OnTargetSubmited(ViGEmTarget target)
        {
            DSUServer?.SubmitReport(target);
        }

        private void OnDSUStopped(DSUServer server)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = false;
            pipeServer.SendMessage(new PipeServerSettings() { settings = new Dictionary<string, string>() { { "DSUEnabled", DSUEnabled.ToString() } } });
        }

        private void OnDSUStarted(DSUServer server)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = true;
            pipeServer.SendMessage(new PipeServerSettings() { settings = new Dictionary<string, string>() { { "DSUEnabled", DSUEnabled.ToString() } } });
        }

        private void OnClientMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.FORCE_SHUTDOWN:
                    Hidder?.SetCloaking(false);
                    break;

                case PipeCode.CLIENT_PROFILE:
                    PipeClientProfile profile = (PipeClientProfile)message;
                    ProfileUpdated(profile.profile);
                    break;

                case PipeCode.CLIENT_CURSOR:
                    PipeClientCursor cursor = (PipeClientCursor)message;

                    switch (cursor.action)
                    {
                        case 0: // up
                            XInputController.virtualTarget.Touch.OnMouseUp((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                        case 1: // down
                            XInputController.virtualTarget.Touch.OnMouseDown((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                        case 2: // move
                            XInputController.virtualTarget.Touch.OnMouseMove((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                    }
                    break;

                case PipeCode.CLIENT_SCREEN:
                    PipeClientScreen screen = (PipeClientScreen)message;
                    XInputController.virtualTarget.Touch.UpdateRatio(screen.width, screen.height);
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
            }
        }

        private void OnClientDisconnected(object sender)
        {
            XInputController.virtualTarget.Touch.OnMouseUp(-1, -1, 1048576 /* MouseButtons.Left */);
        }

        private void OnClientConnected(object sender)
        {
            // send controller details
            pipeServer.SendMessage(new PipeServerController()
            {
                ProductName = XInputController.Instance.ProductName,
                InstanceGuid = XInputController.Instance.InstanceGuid,
                ProductGuid = XInputController.Instance.ProductGuid,
                ProductIndex = (int)XInputController.UserIndex
            });

            // send server settings
            pipeServer.SendMessage(new PipeServerSettings() { settings = GetSettings() });
        }

        internal void ProfileUpdated(Profile profile)
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

                    logger.LogInformation("{0} set to {1}", name, property.ToString());
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
                        Hidder.SetCloaking((bool)value);
                        HIDcloaked = (bool)value;
                        break;
                    case "HIDuncloakonclose":
                        HIDuncloakonclose = (bool)value;
                        break;
                    case "HIDmode":
                        UpdateVirtualController((HIDmode)value);
                        break;
                    case "HIDrate":
                        XInputController.virtualTarget?.SetPollRate((int)value);
                        break;
                    case "HIDstrength":
                        XInputController.virtualTarget?.SetVibrationStrength((int)value);
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
            // start the DSUClient
            if (DSUEnabled) DSUServer.Start();

            // turn on the cloaking
            Hidder.SetCloaking(HIDcloaked);

            UpdateVirtualController(HIDmode);
            XInputController.virtualTarget?.SetVibrationStrength(HIDstrength);

            // start the Pipe Server
            pipeServer.Start();

            // start and stop Profile Manager
            profileManager.Start("Default.json");
            profileManager.Stop();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (XInputController.virtualTarget != null)
                {
                    XInputController.virtualTarget.Disconnected += OnTargetDisconnected;
                    XInputController.virtualTarget.Disconnect();
                    logger.LogInformation("Virtual {0} disconnected", XInputController.virtualTarget);
                }
            }
            catch (Exception) { }

            DSUServer?.Stop();
            Hidder?.SetCloaking(!HIDuncloakonclose);
            pipeServer?.Stop();

            return Task.CompletedTask;
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            foreach (SettingsProperty s in Properties.Settings.Default.Properties)
                settings.Add(s.Name, Properties.Settings.Default[s.Name].ToString());

            settings.Add("gyrometer", $"{XInputController.Gyrometer.sensor != null}");
            settings.Add("accelerometer", $"{XInputController.Accelerometer.sensor != null}");

            return settings;
        }
    }
}
