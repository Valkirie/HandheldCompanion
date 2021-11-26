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
        public XInputController PhysicalController;
        private IVirtualGamepad VirtualController;
        private XInputGirometer Gyrometer;
        private XInputAccelerometer Accelerometer;
        private ViGEmClient VirtualClient;

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

            // initialize controller
            switch (HIDmode)
            {
                default:
                case "DualShock4Controller":
                    VirtualController = VirtualClient.CreateDualShock4Controller();
                    break;
                case "Xbox360Controller":
                    VirtualController = VirtualClient.CreateXbox360Controller();
                    break;
            }

            if (VirtualController == null)
            {
                logger.LogCritical("No Virtual controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            // prepare physical controller
            DirectInput dinput = new DirectInput();
            IList<DeviceInstance> dinstances = dinput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            for (int i = (int)UserIndex.One; i <= (int)UserIndex.Three; i++)
            {
                XInputController tmpController = new XInputController((UserIndex)i, HIDrate, logger);

                if (tmpController.controller.IsConnected)
                {
                    PhysicalController = tmpController;
                    PhysicalController.instance = dinstances[i];
                    break;
                }
            }

            if (PhysicalController == null)
            {
                logger.LogCritical("No physical controller detected. Application will stop");
                throw new InvalidOperationException();
            }

            // default is 10ms rating
            Gyrometer = new XInputGirometer(logger);
            if (Gyrometer.sensor == null)
                logger.LogWarning("No Gyrometer detected");

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(logger);
            if (Accelerometer.sensor == null)
                logger.LogWarning("No Accelerometer detected");

            // initialize DSUClient
            DSUServer = new DSUServer(DSUip, DSUport, logger);

            // initialize PipeServer
            PipeServer = new PipeServer("ControllerService", this, logger);
        }

        internal void UpdateProfile(Dictionary<string, string> args)
        {
            foreach (KeyValuePair<string, string> pair in args)
            {
                string name = pair.Key;
                string value = pair.Value;

                switch (name)
                {
                    case "muted":
                        PhysicalController.muted = bool.Parse(value);
                        break;
                    case "accelerometer":
                        PhysicalController.accelerometer.multiplier = float.Parse(value);
                        break;
                    case "gyrometer":
                        PhysicalController.gyrometer.multiplier = float.Parse(value);
                        break;
                }
            }
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            foreach(KeyValuePair<string, string> pair in args)
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
                        PhysicalController.SetPollRate((int)value);
                        break;
                    case "HIDstrength":
                        PhysicalController.SetVibrationStrength((int)value);
                        break;
                    case "DSUEnabled":
                        switch((bool)value)
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

            VirtualController.Connect();
            logger.LogInformation("Virtual {0} connected", VirtualController.GetType().Name);

            PhysicalController.SetDSUServer(DSUServer);
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            PhysicalController.SetVibrationStrength(HIDstrength);

            // start the Pipe Server
            PipeServer.Start();

            // send notification
            PipeServer.SendMessage(new PipeMessage {
                Code = PipeCode.SERVER_TOAST,
                args = new Dictionary<string, string>
                {
                    { "title", $"{VirtualController.GetType().Name}" },
                    { "content", "Virtual device is now connected"}
                }
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (VirtualController != null)
                {
                    VirtualController.Disconnect();
                    logger.LogInformation("Virtual {0} disconnected", VirtualController.GetType().Name);

                    // send notification
                    PipeServer.SendMessage(new PipeMessage
                    {
                        Code = PipeCode.SERVER_TOAST,
                        args = new Dictionary<string, string>
                        {
                            { "title", $"{VirtualController.GetType().Name}" },
                            { "content", "Virtual device is now disconnected"}
                        }
                    });
                }
            }
            catch (Exception) { }

            if (DSUServer != null)
                DSUServer.Stop();

            if (Hidder != null && HIDuncloakonclose)
                Hidder.SetCloaking(false);

            if (PipeServer != null)
                PipeServer.Stop();

            return Task.CompletedTask;
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();

            foreach(SettingsProperty s in Properties.Settings.Default.Properties)
                settings.Add(s.Name, Properties.Settings.Default[s.Name].ToString());

            settings.Add("gyrometer", $"{PhysicalController.gyrometer.sensor != null}");
            settings.Add("accelerometer", $"{PhysicalController.accelerometer.sensor != null}");

            return settings;
        }
    }
}
