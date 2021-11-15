using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public class ControllerService : IHostedService
    {
        // controllers vars
        public XInputController PhysicalController;
        private IDualShock4Controller VirtualController;
        private XInputGirometer Gyrometer;
        private XInputAccelerometer Accelerometer;

        private static PipeServer PipeServer;

        private static DSUServer DSUServer;
        public static HidHide Hidder;

        public static string CurrentExe, CurrentPath, CurrentPathCli, CurrentPathProfiles, CurrentPathHelper, CurrentPathDep;

        private static Timer MonitorTimer;

        public static ProfileManager CurrentManager;
        public static Assembly CurrentAssembly;

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
            CurrentPathHelper = Path.Combine(CurrentPath, "ControllerHelper.exe");
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // initialize log
            logger.LogInformation($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");

            if (!File.Exists(CurrentPathCli))
            {
                logger.LogError("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

            if (!File.Exists(CurrentPathHelper))
            {
                logger.LogError("Controller Helper is missing. Application will stop.");
                throw new InvalidOperationException();
            }

            // initialize PipeServer and PipeClient
            PipeServer = new PipeServer("ControllerService", this, logger);

            // initialize HidHide
            Hidder = new HidHide(CurrentPathCli, logger);
            Hidder.RegisterApplication(CurrentExe);
            Hidder.GetDevices();
            Hidder.HideDevices();

            // initialize Profile Manager
            CurrentManager = new ProfileManager(CurrentPathProfiles, CurrentExe, logger);

            // initialize ViGem
            try
            {
                ViGEmClient client = new ViGEmClient();
                VirtualController = client.CreateDualShock4Controller();

                if (VirtualController == null)
                {
                    logger.LogError("No Virtual controller detected. Application will stop.");
                    throw new InvalidOperationException();
                }
            }
            catch (Exception)
            {
                logger.LogError("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                throw new InvalidOperationException();
            }

            // prepare physical controller
            for (int i = (int)UserIndex.One; i <= (int)UserIndex.Three; i++)
            {
                XInputController tmpController = new XInputController((UserIndex)i);
                if (tmpController.controller.IsConnected)
                    PhysicalController = tmpController;
            }

            if (PhysicalController == null)
            {
                logger.LogError("No physical controller detected. Application will stop.");
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

            // monitors processes and settings
            MonitorTimer = new Timer(1000) { Enabled = false, AutoReset = true };
            MonitorTimer.Elapsed += MonitorHelper;
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

        private void MonitorHelper(object sender, ElapsedEventArgs e)
        {
            // check if PipeServer is connected
            if (PipeServer.connected)
                return;

            // check if Controller Service Helper is running
            Process[] pname = Process.GetProcessesByName("ControllerHelper");
            if (pname.Length != 0)
                return;

            // start Controller Service Helper            
            ControllerClient.CreateHelper();

            logger.LogInformation("Controller Helper has started.");
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // start the DSUClient
            DSUServer.Start(26760);
            PhysicalController.SetDSUServer(DSUServer);

            // start PipeServer
            PipeServer.Start();
            MonitorTimer.Start();

            // turn on the cloaking
            Hidder.SetCloaking(true);
            logger.LogInformation($"Cloaking {PhysicalController.GetType().Name}");

            // plug the virtual controller
            VirtualController.Connect();
            logger.LogInformation($"Virtual {VirtualController.GetType().Name} connected.");

            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);

            logger.LogInformation($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");

            // send notification
            PipeServer.SendMessage(new PipeMessage { Code = PipeCode.CODE_TOAST, args = new string[] { "DualShock 4 Controller", "Virtual device is now connected" } });
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
                    PipeServer.SendMessage(new PipeMessage { Code = PipeCode.CODE_TOAST, args = new string[] { "DualShock 4 Controller", "Virtual device is now disconnected" } });
                }
            }
            catch (Exception) { }

            if (DSUServer != null)
            {
                DSUServer.Stop();
                logger.LogInformation($"DSU Server has stopped.");
            }

            if (Hidder != null)
            {
                Hidder.SetCloaking(false);
                logger.LogInformation($"Uncloaking {PhysicalController.GetType().Name}");
            }

            if (MonitorTimer.Enabled)
                MonitorTimer.Stop();

            PipeServer.Stop();

            return Task.CompletedTask;
        }
    }
}
