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
        public static HidHide Hidder;

        public static string CurrentExe, CurrentPath, CurrentPathCli, CurrentPathProfiles, CurrentPathDep;

        private string HIDmode;
        private bool HIDcloaked, DSUEnabled;
        private int DSUport;

        public ProfileManager CurrentManager;
        public Assembly CurrentAssembly;

        private readonly ILogger<ControllerService> logger;

        public ControllerService(ILogger<ControllerService> logger)
        {
            this.logger = logger;

            CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // settings
            HIDcloaked = Properties.Settings.Default.HIDcloaked;
            HIDmode = Properties.Settings.Default.HIDmode;
            DSUEnabled = Properties.Settings.Default.DSUEnabled;
            DSUport = Properties.Settings.Default.DSUport;

            // initialize log
            logger.LogInformation($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathCli))
            {
                logger.LogCritical("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

            // verifying ViGEm is installed
            try
            {
                VirtualClient = new ViGEmClient();
            }
            catch (Exception)
            {
                logger.LogCritical("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            Hidder = new HidHide(CurrentPathCli, logger);
            Hidder.RegisterApplication(CurrentExe);

            // initialize Profile Manager
            CurrentManager = new ProfileManager(CurrentPathProfiles, CurrentExe, logger);

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
                logger.LogCritical("No Virtual controller detected. Application will stop.");
                throw new InvalidOperationException();
            }

            // prepare physical controller
            DirectInput dinput = new DirectInput();
            IList<DeviceInstance> dinstances = dinput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            for (int i = (int)UserIndex.One; i <= (int)UserIndex.Three; i++)
            {
                XInputController tmpController = new XInputController((UserIndex)i);

                if (tmpController.controller.IsConnected)
                {
                    PhysicalController = tmpController;
                    PhysicalController.instance = dinstances[i];
                    break;
                }
            }

            if (PhysicalController == null)
            {
                logger.LogCritical("No physical controller detected. Application will stop.");
                throw new InvalidOperationException();
            }

            // default is 10ms rating
            Gyrometer = new XInputGirometer(logger);
            if (Gyrometer.sensor == null)
                logger.LogWarning("No Gyrometer detected.");

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(logger);
            if (Accelerometer.sensor == null)
                logger.LogWarning("No Accelerometer detected.");

            // initialize DSUClient
            DSUServer = new DSUServer(logger);

            // initialize PipeServer
            PipeServer = new PipeServer("ControllerService", this, logger);
            PipeServer.Start();
        }

        public void UpdateProcess(int ProcessId, string ProcessPath)
        {
            try
            {
                string ProcessExec = Path.GetFileName(ProcessPath);

                if (CurrentManager.profiles.ContainsKey(ProcessExec))
                {
                    Profile CurrentProfile = CurrentManager.profiles[ProcessExec];
                    CurrentProfile.path = ProcessPath; // update path
                    CurrentProfile.Serialize();

                    PhysicalController.muted = CurrentProfile.whitelisted;
                    PhysicalController.accelerometer.multiplier = CurrentProfile.accelerometer;
                    PhysicalController.gyrometer.multiplier = CurrentProfile.gyrometer;

                    logger.LogInformation($"Profile {CurrentProfile.name} applied.");
                }
                else
                {
                    PhysicalController.muted = false;
                    PhysicalController.accelerometer.multiplier = 1.0f;
                    PhysicalController.gyrometer.multiplier = 1.0f;
                }
            }
            catch (Exception) { }
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            foreach(KeyValuePair<string, string> pair in args)
            {
                string name = pair.Key;
                string value = pair.Value;

                SettingsProperty setting = Properties.Settings.Default.Properties[name];

                if (setting == null)
                    continue;

                object OldValue = setting.DefaultValue;
                object NewValue = OldValue;

                TypeCode typeCode = Type.GetTypeCode(setting.PropertyType);
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        NewValue = bool.Parse(value);
                        break;
                    case TypeCode.Single:
                    case TypeCode.Decimal:
                        NewValue = float.Parse(value);
                        break;
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        NewValue = int.Parse(value);
                        break;
                    case TypeCode.UInt16:
                    case TypeCode.UInt32:
                    case TypeCode.UInt64:
                        NewValue = uint.Parse(value);
                        break;
                    default:
                        NewValue = value;
                        break;
                }

                Properties.Settings.Default[name] = NewValue;
                ApplySetting(name, OldValue, NewValue);
            }

            Properties.Settings.Default.Save();
        }

        private void ApplySetting(string name, object OldValue, object NewValue)
        {
            if (OldValue != NewValue)
            {
                switch(name)
                {
                    case "HIDcloaked":
                        Hidder.SetCloaking((bool)NewValue);
                        logger.LogInformation($"Uncloaking {PhysicalController.GetType().Name}");
                        break;
                    case "HIDmode":
                        // todo
                        break;
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // start the DSUClient
            if (DSUEnabled)
                DSUServer.Start(DSUport);

            // turn on the cloaking
            Hidder.SetCloaking(HIDcloaked);
            logger.LogInformation($"Cloaking {PhysicalController.GetType().Name}");

            VirtualController.Connect();
            logger.LogInformation($"Virtual {VirtualController.GetType().Name} connected.");

            PhysicalController.SetDSUServer(DSUServer);
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);

            logger.LogInformation($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.instance.InstanceName} on slot {PhysicalController.index}.");

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
                    logger.LogInformation($"Virtual {VirtualController.GetType().Name} disconnected.");

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
            {
                DSUServer.Stop();
                logger.LogInformation($"DSU Server has stopped.");
            }

            // uncloak on shutdown !?
            if (Hidder != null)
            {
                Hidder.SetCloaking(false);
                logger.LogInformation($"Uncloaking {PhysicalController.GetType().Name}");
            }
            PipeServer.Stop();

            return Task.CompletedTask;
        }

        public Dictionary<string, string> GetSettings()
        {
            Dictionary<string, string> settings = new Dictionary<string, string>();
            foreach(SettingsProperty s in Properties.Settings.Default.Properties)
                settings.Add(s.Name, s.DefaultValue.ToString());
            return settings;
        }
    }
}
