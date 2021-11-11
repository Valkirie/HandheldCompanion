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
using static ControllerService.ControllerClient;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public class ControllerService : IHostedService
    {
        // controllers vars
        private static XInputController PhysicalController;
        private static IDualShock4Controller VirtualController;
        private static XInputGirometer Gyrometer;
        private static XInputAccelerometer Accelerometer;
        private static DS4Touch DS4Touch;

        private static DSUServer DSUServer;
        public static HidHide Hidder;

        public static string CurrentPath, CurrentPathCli, CurrentPathProfiles, CurrentPathClient, CurrentPathDep;

        private static int CurrenthProcess;
        private static Timer UpdateMonitor;

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
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathClient = Path.Combine(CurrentPath, "ControllerServiceClient.exe");
            CurrentPathDep = Path.Combine(CurrentPath, "dependencies");

            // initialize log
            string ServiceName = nameof(ControllerService);

            logger.LogInformation($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");

            if (!File.Exists(CurrentPathCli))
            {
                logger.LogError("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                throw new InvalidOperationException();
            }

            if (!File.Exists(CurrentPathClient))
            {
                logger.LogError("CurrentPathClient is missing. Application will stop.");
                throw new InvalidOperationException();
            }

            // initialize HidHide
            Hidder = new HidHide(CurrentPathCli, logger);
            Hidder.RegisterApplication(Process.GetCurrentProcess().MainModule.FileName);
            Hidder.GetDevices();
            Hidder.HideDevices();

            // initialize Profile Manager
            CurrentManager = new ProfileManager(CurrentPathProfiles, Process.GetCurrentProcess().MainModule.FileName);

            // initialize ViGem
            try
            {
                ViGEmClient client = new ViGEmClient();

                // 0x05C4 (Original V1)
                // 0x09CC (Pro V2)

                // VirtualController = client.CreateDualShock4Controller(0x054C, 0x09CC);
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

            // initialize DS4Touch
            DS4Touch = new DS4Touch();

            // initialize DSUClient
            DSUServer = new DSUServer();

            // monitor processes and settings
            UpdateMonitor = new Timer(1000) { Enabled = false, AutoReset = true };
            UpdateMonitor.Elapsed += MonitorProcess;

            SendToast("DualShock 4 Controller", "Virtual device is now connected");
        }

        private void MonitorProcess(object sender, ElapsedEventArgs e)
        {
            int ProcessId = GetProcessIdByPath();
            if (ProcessId != CurrenthProcess)
            {
                try
                {
                    Process CurrentProcess = Process.GetProcessById(ProcessId);
                    string ProcessPath = Utils.GetMainModuleFilepath(ProcessId);
                    string ProcessName = Path.GetFileName(ProcessPath);

                    if (CurrentManager.profiles.ContainsKey(ProcessName))
                    {
                        // muting process
                        Profile CurrentProfile = CurrentManager.profiles[ProcessName];
                        PhysicalController.muted = CurrentProfile.whitelisted;
                        PhysicalController.accelerometer.multiplier = CurrentProfile.accelerometer;
                        logger.LogInformation($"Profile {CurrentProfile.name} applied.");
                    }
                    else
                    {
                        PhysicalController.muted = false;
                        PhysicalController.accelerometer.multiplier = 1.0f;
                    }
                }
                catch (Exception) { }

                CurrenthProcess = ProcessId;
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // start the DSUClient
            if (DSUServer != null)
            {
                logger.LogInformation($"DSU Server has started. Listening to port: {26760}");
                DSUServer.Start(26760);
                PhysicalController.SetDSUServer(DSUServer);
            }

            // start monitoring processes
            UpdateMonitor.Enabled = true;
            UpdateMonitor.Start();

            // turn on the cloaking
            Hidder.SetCloaking(true);
            logger.LogInformation($"Cloaking {PhysicalController.GetType().Name}");

            // plug the virtual controller
            VirtualController.Connect();
            logger.LogInformation($"Virtual {VirtualController.GetType().Name} connected.");

            PhysicalController.SetTouch(DS4Touch);
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            logger.LogInformation($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (VirtualController != null)
                {
                    VirtualController.Disconnect();
                    logger.LogInformation($"Virtual {VirtualController.GetType().Name} disconnected.");
                }
            }
            catch (Exception) { }

            if (DSUServer != null)
            {
                DSUServer.Stop();
                logger.LogInformation($"DSU Server has stopped.");
            }

            if (Hidder != null)
                Hidder.SetCloaking(false);

            if (UpdateMonitor.Enabled)
                UpdateMonitor.Stop();

            DS4Touch.Stop();

            logger.LogInformation($"Uncloaking {PhysicalController.GetType().Name}");

            SendToast("DualShock 4 Controller", "Virtual device is now disconnected");

            return Task.CompletedTask;
        }
    }
}
