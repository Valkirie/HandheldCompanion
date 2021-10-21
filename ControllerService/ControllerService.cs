using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControllerService
{
    public partial class ControllerService : ServiceBase
    {
        // controllers vars
        private static XInputController PhysicalController;
        private static IDualShock4Controller VirtualController;
        private static XInputGirometer Gyrometer;
        private static XInputAccelerometer Accelerometer;
        private static UdpServer UDPServer;
        public static HidHide Hidder;

        public static string CurrentPath, CurrentPathCli, CurrentPathProfiles;
        private static bool IsRunning;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(int hWnd, out uint lpdwProcessId);
        private static int CurrenthProcess;
        private static Thread MonitorThread;

        public static ProfileManager CurrentManager;

        public ControllerService(string[] args)
        {
            InitializeComponent();

            // initialize log
            string ServiceName = this.ServiceName;
            RegistryKey key = Registry.LocalMachine.CreateSubKey($"SYSTEM\\CurrentControlSet\\Services\\EventLog\\Application\\{ServiceName}");

            eventLog1.Source = ServiceName;
            if (!EventLog.SourceExists(eventLog1.Source))
                EventLog.CreateEventSource(eventLog1.Source, eventLog1.Source);

            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            eventLog1.WriteEntry($"AyaGyroAiming ({fileVersionInfo.ProductVersion})");

            // paths
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathCli = @"C:\Program Files\Nefarius Software Solutions e.U\HidHideCLI\HidHideCLI.exe";
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");

            if (!File.Exists(CurrentPathCli))
            {
                eventLog1.WriteEntry("HidHide is missing. Please get it from: https://github.com/ViGEm/HidHide/releases");
                this.Stop();
            }

            // initialize HidHide
            Hidder = new HidHide(CurrentPathCli);
            Hidder.RegisterApplication(Assembly.GetExecutingAssembly().Location);

            // initialize Profile Manager
            CurrentManager = new ProfileManager(CurrentPathProfiles);

            // initialize ViGem
            try
            {
                ViGEmClient client = new ViGEmClient();
                VirtualController = client.CreateDualShock4Controller();

                if (VirtualController == null)
                {
                    eventLog1.WriteEntry("No Virtual controller detected. Application will stop.");
                    this.Stop();
                }
            }
            catch (Exception)
            {
                eventLog1.WriteEntry("ViGEm is missing. Please get it from: https://github.com/ViGEm/ViGEmBus/releases");
                this.Stop();
            }

            // prepare physical controller
            for (int i = 0; i < 4; i++)
            {
                PhysicalController = new XInputController((SharpDX.XInput.UserIndex)i);
                if (PhysicalController.connected)
                    break; // got it !
            }

            if (PhysicalController == null)
            {
                eventLog1.WriteEntry("No physical controller detected. Application will stop.");
                this.Stop();
            }

            // hide the physical controller
            foreach (Device d in Hidder.GetDevices().Where(a => a.gamingDevice))
            {
                string VID = Utils.Between(d.deviceInstancePath.ToLower(), "vid_", "&");
                string PID = Utils.Between(d.deviceInstancePath.ToLower(), "pid_", "&");

                string query = $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE \"%VID_{VID}&PID_{PID}%\"";

                var moSearch = new ManagementObjectSearcher(query);
                var moCollection = moSearch.Get();

                foreach (ManagementObject mo in moCollection)
                {
                    foreach (var item in mo.Properties)
                    {
                        if (item.Name == "DeviceID")
                        {
                            string DeviceID = ((string)item.Value);
                            Hidder.HideDevice(DeviceID);
                            Hidder.HideDevice(d.deviceInstancePath);
                            eventLog1.WriteEntry($"HideDevice hidding {DeviceID}");
                            break;
                        }
                    }
                }
            }

            // default is 10ms rating and 10 samples
            Gyrometer = new XInputGirometer(eventLog1);
            if (Gyrometer.sensor == null)
                eventLog1.WriteEntry("No Gyrometer detected.");

            // default is 10ms rating
            Accelerometer = new XInputAccelerometer(eventLog1);
            if (Accelerometer.sensor == null)
                eventLog1.WriteEntry("No Accelerometer detected.");

            // initialize UDP Server
            UDPServer = new UdpServer();

            // monitor processes and settings
            MonitorThread = new Thread(MonitorProcess);

            // todo : Configurez le mécanisme d’interrogation
        }

        static void MonitorProcess()
        {
            while (IsRunning)
            {
                int ProcessId = Utils.GetProcessIdByPath(CurrentPath + "ControllerServiceClient.exe");
                if (ProcessId != CurrenthProcess)
                {
                    try
                    {
                        Process CurrentProcess = Process.GetProcessById((int)ProcessId);
                        Profile CurrentProfile = CurrentManager.profiles[CurrentProcess.ProcessName];
                        PhysicalController.muted = CurrentProfile.whitelisted;
                    }
                    catch (Exception) { }

                    CurrenthProcess = ProcessId;
                }

                Thread.Sleep(1000);
            }
        }

        protected override void OnStart(string[] args)
        {
            IsRunning = true;

            // start the UDP server
            if (UDPServer != null)
            {
                eventLog1.WriteEntry($"UDP server has started. Listening to port: {26760}");
                UDPServer.Start(26760);
                PhysicalController.SetUdpServer(UDPServer);
            }

            // start monitoring processes
            MonitorThread.Start();

            // turn on the cloaking
            Hidder.SetCloaking(true);
            eventLog1.WriteEntry($"Cloaking {PhysicalController.GetType().Name}");

            // plug the virtual controler
            VirtualController.Connect();
            eventLog1.WriteEntry($"Virtual {VirtualController.GetType().Name} connected.");
            PhysicalController.SetVirtualController(VirtualController);
            PhysicalController.SetGyroscope(Gyrometer);
            PhysicalController.SetAccelerometer(Accelerometer);
            eventLog1.WriteEntry($"Virtual {VirtualController.GetType().Name} attached to {PhysicalController.GetType().Name} {PhysicalController.index}.");
        }

        protected override void OnStop()
        {
            IsRunning = false;

            try
            {
                if (VirtualController != null)
                {
                    VirtualController.Disconnect();
                    eventLog1.WriteEntry($"Virtual {VirtualController.GetType().Name} disconnected.");
                }
            }
            catch (Exception) { }

            if (UDPServer != null)
            {
                UDPServer.Stop();
                eventLog1.WriteEntry($"UDP server has stopped.");
            }

            Hidder.SetCloaking(false);
            eventLog1.WriteEntry($"Uncloaking {PhysicalController.GetType().Name}");
        }
    }
}
