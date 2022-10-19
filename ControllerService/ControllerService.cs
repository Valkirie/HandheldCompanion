using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Sensors;
using ControllerCommon.Utils;
using ControllerService.Targets;
using Microsoft.Extensions.Hosting;
using Nefarius.Utilities.DeviceManagement.PnP;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ControllerCommon.Managers.SystemManager;
using static ControllerCommon.Utils.DeviceUtils;
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
        private DSUServer DSUServer;
        public HidHide Hidder;

        // devices vars
        public static Device handheldDevice;
        private UserIndex HIDidx;
        private string deviceInstancePath;
        private string baseContainerDeviceInstancePath;

        public static string CurrentExe, CurrentPath, CurrentPathDep;
        public static string CurrentTag;
        public static int CurrentOverlayStatus = 2;

        // settings vars
        Configuration configuration;
        private string DSUip;
        private bool HIDcloaked, HIDuncloakonclose, DSUEnabled;
        private int DSUport;
        private double HIDstrength;
        private HIDmode HIDmode = HIDmode.NoController;
        private HIDstatus HIDstatus = HIDstatus.Disconnected;

        // sensor vars
        private static SensorFamily SensorSelection;
        private static int SensorPlacement;
        private static bool SensorPlacementUpsideDown;

        // profile vars
        public static Profile currentProfile = new();
        public static Profile defaultProfile = new();

        public ControllerService(IHostApplicationLifetime lifetime)
        {
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
            HIDuncloakonclose = bool.Parse(configuration.AppSettings.Settings["HIDuncloakonclose"].Value);
            HIDmode = Enum.Parse<HIDmode>(configuration.AppSettings.Settings["HIDmode"].Value);
            HIDstatus = Enum.Parse<HIDstatus>(configuration.AppSettings.Settings["HIDstatus"].Value);
            DSUEnabled = bool.Parse(configuration.AppSettings.Settings["DSUEnabled"].Value);
            DSUip = configuration.AppSettings.Settings["DSUip"].Value;
            DSUport = int.Parse(configuration.AppSettings.Settings["DSUport"].Value);
            HIDstrength = double.Parse(configuration.AppSettings.Settings["HIDstrength"].Value);

            SensorSelection = Enum.Parse<SensorFamily>(configuration.AppSettings.Settings["SensorSelection"].Value);
            SensorPlacement = int.Parse(configuration.AppSettings.Settings["SensorPlacement"].Value);
            SensorPlacementUpsideDown = bool.Parse(configuration.AppSettings.Settings["SensorPlacementUpsideDown"].Value);

            HIDidx = Enum.Parse<UserIndex>(configuration.AppSettings.Settings["HIDidx"].Value);
            deviceInstancePath = configuration.AppSettings.Settings["deviceInstancePath"].Value;
            baseContainerDeviceInstancePath = configuration.AppSettings.Settings["baseContainerDeviceInstancePath"].Value;

            // verifying ViGEm is installed
            try
            {
                VirtualClient = new ViGEmClient();
            }
            catch (Exception)
            {
                LogManager.LogCritical("ViGEm is missing. Please get it from: {0}", "https://github.com/ViGEm/ViGEmBus/releases");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            Hidder = new HidHide();
            Hidder.RegisterApplication(CurrentExe);

            // initialize PipeServer
            pipeServer = new PipeServer("ControllerService");
            pipeServer.Connected += OnClientConnected;
            pipeServer.Disconnected += OnClientDisconnected;
            pipeServer.ClientMessage += OnClientMessage;

            // initialize manager(s)
            SystemManager.SerialArrived += SystemManager_SerialArrived;
            SystemManager.SerialRemoved += SystemManager_SerialRemoved;
            SystemManager.Start();
            SystemManager_SerialArrived(null);

            // initialize device
            handheldDevice = Device.GetDefault();

            // XInputController settings
            XInputController = new XInputController(SensorSelection, pipeServer);
            XInputController.SetVibrationStrength(HIDstrength);
            XInputController.Updated += OnTargetSubmited;

            // prepare physical controller
            SetControllerIdx(HIDidx, deviceInstancePath, baseContainerDeviceInstancePath);

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport);
            DSUServer.Started += OnDSUStarted;
            DSUServer.Stopped += OnDSUStopped;
        }

        private SerialUSBIMU sensor;
        private void SystemManager_SerialArrived(PnPDevice device)
        {
            switch (SensorSelection)
            {
                case SensorFamily.SerialUSBIMU:
                    {
                        sensor = SerialUSBIMU.GetDefault();

                        if (sensor is null)
                            break;

                        sensor.Open();
                        sensor.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);

                        XInputController?.UpdateSensors();
                    }
                    break;
            }
        }

        private void SystemManager_SerialRemoved(PnPDevice device)
        {
            switch (SensorSelection)
            {
                case SensorFamily.SerialUSBIMU:
                    {
                        if (sensor is null)
                            break;

                        sensor.Close();
                        XInputController?.UpdateSensors();
                    }
                    break;
            }
        }

        private void SetControllerIdx(UserIndex idx, string deviceInstancePath, string baseContainerDeviceInstancePath)
        {
            ControllerEx controller = new ControllerEx(idx);
            controller.deviceInstancePath = deviceInstancePath;
            controller.baseContainerDeviceInstancePath = baseContainerDeviceInstancePath;

            XInputController.SetController(controller);

            if (!controller.IsConnected())
            {
                LogManager.LogWarning("No physical controller detected on UserIndex: {0}", idx);
                return;
            }

            if (this.deviceInstancePath == deviceInstancePath &&
                this.baseContainerDeviceInstancePath == baseContainerDeviceInstancePath)
                return;

            LogManager.LogInformation("Listening to physical controller on UserIndex: {0}", idx);

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

            // update variables
            this.HIDidx = idx;
            this.deviceInstancePath = deviceInstancePath;
            this.baseContainerDeviceInstancePath = baseContainerDeviceInstancePath;

            // update settings
            configuration.AppSettings.Settings["HIDidx"].Value = ((int)idx).ToString();
            configuration.AppSettings.Settings["deviceInstancePath"].Value = deviceInstancePath;
            configuration.AppSettings.Settings["baseContainerDeviceInstancePath"].Value = baseContainerDeviceInstancePath;
            configuration.Save(ConfigurationSaveMode.Modified);
        }

        private void SetControllerMode(HIDmode mode)
        {
            if (HIDmode == mode && VirtualTarget != null)
                return;

            // disconnect current virtual controller
            // todo: do not disconnect if similar to incoming mode
            VirtualTarget?.Disconnect();

            switch (mode)
            {
                default:
                case HIDmode.NoController:
                    VirtualTarget.Dispose();
                    VirtualTarget = null;
                    XInputController.DetachTarget();
                    break;
                case HIDmode.DualShock4Controller:
                    VirtualTarget = new DualShock4Target(XInputController, VirtualClient);
                    break;
                case HIDmode.Xbox360Controller:
                    VirtualTarget = new Xbox360Target(XInputController, VirtualClient);
                    break;
            }

            if (VirtualTarget != null)
            {
                VirtualTarget.Connected += OnTargetConnected;
                VirtualTarget.Disconnected += OnTargetDisconnected;
                XInputController.AttachTarget(VirtualTarget);
            }

            // update status
            SetControllerStatus(HIDstatus);

            // update value
            HIDmode = mode;
        }

        private void SetControllerStatus(HIDstatus status)
        {
            if (VirtualTarget == null)
                return;

            switch (status)
            {
                default:
                case HIDstatus.Connected:
                    VirtualTarget.Connect();
                    break;
                case HIDstatus.Disconnected:
                    VirtualTarget.Disconnect();
                    break;
            }

            // update value
            HIDstatus = status;
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
                    ProfileUpdated(profile.profile, profile.backgroundTask);
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

                case PipeCode.CLIENT_INPUT:
                    PipeClientInput input = (PipeClientInput)message;
                    VirtualTarget?.InjectReport((GamepadButtonFlagsExt)input.Buttons, input.sButtons);
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
            // send server settings
            pipeServer.SendMessage(new PipeServerSettings() { settings = GetSettings() });
        }

        internal void ProfileUpdated(Profile profile, bool backgroundtask)
        {
            // skip if current profile
            if (profile == currentProfile)
                return;

            // restore default profile
            if (profile == null || !profile.isEnabled)
                profile = defaultProfile;

            // update current profile
            currentProfile = profile;

            // update default profile
            if (profile.isDefault)
                defaultProfile = profile;
            else
                LogManager.LogInformation("Profile {0} applied.", profile.name);
        }

        public void UpdateSettings(Dictionary<string, object> args)
        {
            foreach (KeyValuePair<string, object> pair in args)
            {
                string name = pair.Key;
                string property = pair.Value.ToString();

                if (configuration.AppSettings.Settings.AllKeys.ToList().Contains(name))
                {
                    configuration.AppSettings.Settings[name].Value = property;
                    configuration.Save(ConfigurationSaveMode.Modified);
                }

                ApplySetting(name, property);
                LogManager.LogDebug("{0} set to {1}", name, property);
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
                case "SensorPlacement":
                    {
                        int value = int.Parse(property);
                        SensorPlacement = value;
                        sensor?.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);
                    }
                    break;
                case "SensorPlacementUpsideDown":
                    {
                        bool value = bool.Parse(property);
                        SensorPlacementUpsideDown = value;
                        sensor?.SetSensorPlacement((SerialPlacement)SensorPlacement, SensorPlacementUpsideDown);
                    }
                    break;
                    /* case "SensorSelection":
                        {
                            SensorFamily value = Enum.Parse<SensorFamily>(property);
                            SensorSelection = value;
                        }
                        break; */
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // start listening to controller
            XInputController.StartListening();

            // turn on cloaking
            Hidder.SetCloaking(HIDcloaked, XInputController.ProductName);

            // start DSUClient
            if (DSUEnabled) DSUServer.Start();

            // update virtual controller
            SetControllerMode(HIDmode);

            // start Pipe Server
            pipeServer.Open();

            // listen to system events
            SystemManager.SystemStatusChanged += OnSystemStatusChanged;

            // OnPowerChange(null, new PowerModeChangedEventArgs(PowerModes.Suspend));
            // OnPowerChange(null, new PowerModeChangedEventArgs(PowerModes.Resume));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // stop listening from controller
            XInputController.StopListening();

            // turn off cloaking
            Hidder?.SetCloaking(!HIDuncloakonclose, XInputController.ProductName);

            // update virtual controller
            SetControllerMode(HIDmode.NoController);

            // stop listening to system events
            SystemManager.SystemStatusChanged -= OnSystemStatusChanged;

            // stop DSUClient
            DSUServer?.Stop();

            // stop Pipe Server
            pipeServer?.Close();

            // stop System Manager
            SystemManager.Stop();

            return Task.CompletedTask;
        }

        private async void OnSystemStatusChanged(SystemStatus status)
        {
            LogManager.LogInformation("System status set to {0}", status);

            switch (status)
            {
                case SystemStatus.Ready:
                    {
                        // resume delay (arbitrary)
                        await Task.Delay(4000);

                        // (re)initialize sensors
                        XInputController?.UpdateSensors();

                        // (re)initialize ViGEm
                        VirtualClient = new ViGEmClient();

                        SetControllerMode(HIDmode);
                    }
                    break;
                case SystemStatus.Unready:
                    {
                        XInputController?.StopListening();

                        VirtualTarget.Dispose();
                        VirtualTarget = null;

                        VirtualClient.Dispose();
                        VirtualClient = null;
                    }
                    break;
            }
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
