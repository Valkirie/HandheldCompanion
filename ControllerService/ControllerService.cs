using ControllerCommon;
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
        public XInputController XInputController;
        private XInputGirometer Gyrometer;
        private XInputAccelerometer Accelerometer;

        private PipeServer PipeServer;
        private DSUServer DSUServer;
        public HidHide Hidder;

        public static string CurrentExe, CurrentPath, CurrentPathCli, CurrentPathDep;

        private string DSUip, HIDmode;
        private bool HIDcloaked, HIDuncloakonclose, DSUEnabled;
        private int DSUport, HIDrate, HIDstrength;

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
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // settings
            HIDcloaked = Properties.Settings.Default.HIDcloaked;
            HIDuncloakonclose = Properties.Settings.Default.HIDuncloakonclose;
            HIDmode = Properties.Settings.Default.HIDmode;
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

                XInputController = new XInputController(controller, idx, HIDrate, HIDmode, logger);
                XInputController.instance = dinstances[(int)idx];
                break;
            }

            if (XInputController == null)
            {
                logger.LogCritical("No physical controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            // default is 10ms rating
            Gyrometer = new XInputGirometer(XInputController, logger);
            if (Gyrometer.sensor == null)
                logger.LogWarning("No Gyrometer detected");

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(XInputController, logger);
            if (Accelerometer.sensor == null)
                logger.LogWarning("No Accelerometer detected");

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport, logger);
            DSUServer.Started += OnDSUStarted;
            DSUServer.Stopped += OnDSUStopped;

            // initialize PipeServer
            PipeServer = new PipeServer("ControllerService", logger);
            PipeServer.Connected += OnClientConnected;
            PipeServer.Disconnected += OnClientDisconnected;
            PipeServer.ClientMessage += OnClientMessage;
        }

        private void OnDSUStopped(object sender)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = false;
            PipeServer.SendMessage(new PipeServerSettings() { settings = new Dictionary<string, string>() { { "DSUEnabled", DSUEnabled.ToString() } } });
        }

        private void OnDSUStarted(object sender)
        {
            DSUEnabled = Properties.Settings.Default.DSUEnabled = true;
            PipeServer.SendMessage(new PipeServerSettings() { settings = new Dictionary<string, string>() { { "DSUEnabled", DSUEnabled.ToString() } } });
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
                    UpdateProfile(profile.profile);
                    break;

                case PipeCode.CLIENT_CURSOR:
                    PipeClientCursor cursor = (PipeClientCursor)message;

                    switch (cursor.action)
                    {
                        case 0: // up
                            XInputController.target.touch.OnMouseUp((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                        case 1: // down
                            XInputController.target.touch.OnMouseDown((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                        case 2: // move
                            XInputController.target.touch.OnMouseMove((short)cursor.x, (short)cursor.y, cursor.button);
                            break;
                    }
                    break;

                case PipeCode.CLIENT_SCREEN:
                    PipeClientScreen screen = (PipeClientScreen)message;
                    XInputController.target.touch.UpdateRatio(screen.width, screen.height);
                    break;

                case PipeCode.CLIENT_SETTINGS:
                    PipeClientSettings settings = (PipeClientSettings)message;
                    UpdateSettings(settings.settings);
                    break;

                case PipeCode.CLIENT_HIDDER:
                    PipeClientHidder hidder = (PipeClientHidder)message;

                    switch (hidder.action)
                    {
                        case 0: // reg
                            Hidder.RegisterApplication(hidder.path);
                            break;
                        case 1: // unreg
                            Hidder.UnregisterApplication(hidder.path);
                            break;
                    }
                    break;
            }
        }

        private void OnClientDisconnected(object sender)
        {
            XInputController.target.touch.OnMouseUp(-1, -1, 1048576 /* MouseButtons.Left */);
        }

        private void OnClientConnected(object sender)
        {
            // send controller details
            PipeServer.SendMessage(new PipeServerController()
            {
                ProductName = XInputController.instance.ProductName,
                InstanceGuid = XInputController.instance.InstanceGuid,
                ProductGuid = XInputController.instance.ProductGuid,
                ProductIndex = (int)XInputController.index
            });

            // send server settings
            PipeServer.SendMessage(new PipeServerSettings() { settings = GetSettings() });
        }

        internal void UpdateProfile(Profile profile)
        {
            XInputController.target.profile = profile;
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            foreach (KeyValuePair<string, string> pair in args)
            {
                string name = pair.Key;
                string property = pair.Value;

                SettingsProperty setting = Properties.Settings.Default.Properties[name];
                if (setting != null)
                {
                    object prev_value = Properties.Settings.Default[name].ToString();
                    object value;

                    TypeCode typeCode = Type.GetTypeCode(setting.PropertyType);
                    switch (typeCode)
                    {
                        case TypeCode.Boolean:
                            value = bool.Parse(property);
                            prev_value = bool.Parse((string)prev_value);
                            break;
                        case TypeCode.Single:
                        case TypeCode.Decimal:
                            value = float.Parse(property);
                            prev_value = float.Parse((string)prev_value);
                            break;
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                            value = int.Parse(property);
                            prev_value = int.Parse((string)prev_value);
                            break;
                        case TypeCode.UInt16:
                        case TypeCode.UInt32:
                        case TypeCode.UInt64:
                            value = uint.Parse(property);
                            prev_value = uint.Parse((string)prev_value);
                            break;
                        default:
                            value = property;
                            prev_value = (string)prev_value;
                            break;
                    }

                    Properties.Settings.Default[name] = value;
                    ApplySetting(name, prev_value, value);
                }
            }

            Properties.Settings.Default.Save();
        }

        private void ApplySetting(string name, object prev_value, object value)
        {
            if (prev_value.ToString() != value.ToString())
            {
                logger.LogInformation("{0} set to {1}", name, value.ToString());

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
                        // todo
                        break;
                    case "HIDrate":
                        XInputController.SetPollRate((int)value);
                        break;
                    case "HIDstrength":
                        XInputController.SetVibrationStrength((int)value);
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
            if (DSUEnabled)
                DSUServer.Start();

            // turn on the cloaking
            Hidder.SetCloaking(HIDcloaked);

            // initialize virtual controller
            XInputController.SetTarget(VirtualClient);
            XInputController.SetDSUServer(DSUServer);
            XInputController.SetGyroscope(Gyrometer);
            XInputController.SetAccelerometer(Accelerometer);
            XInputController.SetVibrationStrength(HIDstrength);

            // start the Pipe Server
            PipeServer.Start();

            // send notification
            PipeServer.SendMessage(new PipeServerToast
            {
                title = $"{XInputController.target.GetType().Name}",
                content = "Virtual device is now connected"
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (XInputController.target != null)
                {
                    XInputController.target.Disconnect();
                    logger.LogInformation("Virtual {0} disconnected", XInputController.target.GetType().Name);

                    // send notification
                    PipeServer.SendMessage(new PipeServerToast
                    {
                        title = $"{XInputController.target.GetType().Name}",
                        content = "Virtual device is now disconnected"
                    });
                }
            }
            catch (Exception) { }

            DSUServer?.Stop();
            Hidder?.SetCloaking(!HIDuncloakonclose);
            PipeServer?.Stop();

            return Task.CompletedTask;
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            foreach (SettingsProperty s in Properties.Settings.Default.Properties)
                settings.Add(s.Name, Properties.Settings.Default[s.Name].ToString());

            settings.Add("gyrometer", $"{XInputController.gyrometer.sensor != null}");
            settings.Add("accelerometer", $"{XInputController.accelerometer.sensor != null}");

            return settings;
        }
    }
}
