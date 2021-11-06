using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.ServiceProcess;
using System.Timers;
using static ControllerService.ControllerClient;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public partial class ControllerService : ServiceBase
    {
        // controllers vars
        private static XInputController PhysicalController;
        private static IDualShock4Controller VirtualController;
        private static XInputGirometer Gyrometer;
        private static XInputAccelerometer Accelerometer;
        private static DSUServer DSUServer;
        public static HidHide Hidder;

        public static string CurrentPath, CurrentPathCli, CurrentPathProfiles, CurrentPathClient, CurrentPathDep;

        private static int CurrenthProcess;
        private static Timer UpdateMonitor;

        public static ProfileManager CurrentManager;
        public static Assembly CurrentAssembly;
        public static EventLog CurrentLog;

        public ControllerService(string[] args)
        {
            InitializeComponent();

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
            string ServiceName = this.ServiceName;
            CurrentLog = new EventLog(CurrentPath);
            RegistryKey key = Registry.LocalMachine.CreateSubKey($"SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\{ServiceName}");

            CurrentLog.Source = $"{ServiceName}.log";
            if (!CurrentLog.SourceExists())
                CurrentLog.CreateEventSource();

            CurrentLog.WriteEntry($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");

            if (!File.Exists(CurrentPathCli))
            {
                CurrentLog.WriteEntry("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                this.Stop();
            }

            if (!File.Exists(CurrentPathClient))
            {
                CurrentLog.WriteEntry("CurrentPathClient is missing. Application will stop.");
                this.Stop();
            }

            // initialize HidHide
            Hidder = new HidHide(CurrentPathCli);
            Hidder.RegisterApplication(CurrentAssembly.Location);
            Hidder.GetDevices();
            Hidder.HideDevices();

            // initialize Profile Manager
            CurrentManager = new ProfileManager(CurrentPathProfiles, CurrentAssembly.Location);

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
                    CurrentLog.WriteEntry("No Virtual controller detected. Application will stop.");
                    this.Stop();
                }
            }
            catch (Exception)
            {
                CurrentLog.WriteEntry("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                this.Stop();
            }

            // prepare physical controller
            for (int i = 0; i < 4; i++)
            {
                XInputController tmpController = new XInputController((SharpDX.XInput.UserIndex)i);
                if (tmpController.controller.IsConnected)
                    PhysicalController = tmpController;
            }

            if (PhysicalController == null)
            {
                CurrentLog.WriteEntry("No physical controller detected. Application will stop.");
                this.Stop();
            }

            // default is 10ms rating
            Gyrometer = new XInputGirometer(CurrentLog);
            if (Gyrometer.sensor == null)
                CurrentLog.WriteEntry("No Gyrometer detected.");

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(CurrentLog);
            if (Accelerometer.sensor == null)
                CurrentLog.WriteEntry("No Accelerometer detected.");

            // initialize DSUClient
            DSUServer = new DSUServer();

            // monitor processes and settings
            UpdateMonitor = new Timer(1000) { Enabled = true, AutoReset = true };
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
                    Process CurrentProcess = Process.GetProcessById((int)ProcessId);
                    string ProcessName = CurrentProcess.ProcessName;

                    if (CurrentManager.profiles.ContainsKey(ProcessName))
                    {
                        // muting process
                        Profile CurrentProfile = CurrentManager.profiles[ProcessName];
                        PhysicalController.muted = CurrentProfile.whitelisted;

                        // wrapper process
                        BinaryType bt;
                        GetBinaryType(CurrentProfile.path, out bt);

                        string wrapperpath = Path.Combine(CurrentPathDep, bt == BinaryType.SCS_64BIT_BINARY ? "x64" : "x86");
                        string wrapperdllpath = Path.Combine(wrapperpath, "xinput1_3.dll");

                        string processpath = Path.GetDirectoryName(CurrentProfile.path);
                        string processdllpath = Path.Combine(processpath, "xinput1_3.dll");

                        bool wrapped = File.Exists(processdllpath);
                        if (CurrentProfile.use_wrapper && !wrapped)
                            File.Copy(wrapperdllpath, processdllpath);
                        else if (!CurrentProfile.use_wrapper && wrapped)
                            File.Delete(processdllpath);
                    }
                    else
                        PhysicalController.muted = false;
                }
                catch (Exception) { }

                CurrenthProcess = ProcessId;
            }
        }

        protected override void OnStart(string[] args)
        {
            // start the DSUClient
            if (DSUServer != null)
            {
                CurrentLog.WriteEntry($"DSU Server has started. Listening to port: {26760}");
                DSUServer.Start(26760);
                PhysicalController.SetDSUServer(DSUServer);
            }

            // start monitoring processes
            UpdateMonitor.Start();

            // turn on the cloaking
            Hidder.SetCloaking(true);
            CurrentLog.WriteEntry($"Cloaking {PhysicalController.GetType().Name}");

            // plug the virtual controler
            VirtualController.Connect();
            CurrentLog.WriteEntry($"Virtual {VirtualController.GetType().Name} connected.");
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            CurrentLog.WriteEntry($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");
            
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            try
            {
                if (VirtualController != null)
                {
                    VirtualController.Disconnect();
                    CurrentLog.WriteEntry($"Virtual {VirtualController.GetType().Name} disconnected.");
                }
            }
            catch (Exception) { }

            if (DSUServer != null)
            {
                DSUServer.Stop();
                CurrentLog.WriteEntry($"DSU Server has stopped.");
            }

            if (Hidder != null)
                Hidder.SetCloaking(false);

            if(UpdateMonitor.Enabled)
                UpdateMonitor.Stop();

            CurrentLog.WriteEntry($"Uncloaking {PhysicalController.GetType().Name}");

            SendToast("DualShock 4 Controller", "Virtual device is now disconnected");

            base.OnStop();
        }
    }
}
