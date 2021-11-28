using ControllerCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace ControllerHelper
{
    public partial class ControllerHelper : Form
    {
        #region imports
        [DllImport("User32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr lpdwProcessId);
        #endregion

        public static PipeClient PipeClient;
        private Timer MonitorTimer;
        private IntPtr CurrentProcess;

        public static Controller CurrentController;

        private MouseHook m_Hook;

        private FormWindowState CurrentWindowState;
        private object updateLock = new();

        private HIDmode HideDS4 = new HIDmode("DualShock4Controller", "DualShock 4 emulation");
        private HIDmode HideXBOX = new HIDmode("Xbox360Controller", "Xbox 360 emulation");
        private Dictionary<string, HIDmode> HIDmodes = new();

        public static string CurrentExe, CurrentPath, CurrentPathService, CurrentPathProfiles, CurrentPathLogs;

        private bool RunAtStartup, StartMinimized, CloseMinimises, HookMouse;
        private bool IsElevated, FirstStart;

        public ProfileManager ProfileManager;
        public ServiceManager ServiceManager;
        private readonly string ServiceName, ServiceDescription;

        // TaskManager vars
        private static Task CurrentTask;

        private readonly ILogger logger;

        public ControllerHelper(ILogger logger)
        {
            InitializeComponent();

            this.logger = logger;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // settings
            IsElevated = Utils.IsAdministrator();
            ServiceName = Properties.Settings.Default.ServiceName;
            ServiceDescription = Properties.Settings.Default.ServiceDescription;
            FirstStart = Properties.Settings.Default.FirstStart;

            // initialize log
            logger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.ProductVersion);

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                logger.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize pipe client
            PipeClient = new PipeClient("ControllerService", logger);
            PipeClient.Disconnected += OnClientDisconnected;
            PipeClient.ServerMessage += OnServerMessage;

            // initialize mouse hook
            m_Hook = new MouseHook(PipeClient, this, logger);

            cB_HidMode.Items.Add(HideDS4);
            cB_HidMode.Items.Add(HideXBOX);

            HIDmodes.Add("DualShock4Controller", HideDS4);
            HIDmodes.Add("Xbox360Controller", HideXBOX);

            if (StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            if (FirstStart)
            {
                DialogResult dr = MessageBox.Show("Dear handheld gamer,\n\nThe service you are about to use was made for free in order to bring the best possible gaming experience out of your device.\n\nIf you are enjoying it, please consider giving back to the author's efforts and show your appreciation through a donation.\n\nHave fun !", "Please, gives us a minute", MessageBoxButtons.YesNo);
                switch (dr)
                {
                    case DialogResult.Yes:
                        Utils.OpenUrl("https://www.paypal.com/paypalme/BenjaminLSR");
                        break;
                    case DialogResult.No:
                        break;
                }
                FirstStart = false;
                Properties.Settings.Default.FirstStart = FirstStart;
                Properties.Settings.Default.Save();
            }
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_PING:
                    PipeServerPing ping = (PipeServerPing)message;
                    UpdateStatus(true);
                    UpdateScreen();
                    break;

                case PipeCode.SERVER_TOAST:
                    PipeServerToast toast = (PipeServerToast)message;
                    Utils.SendToast(toast.title, toast.content);
                    break;

                case PipeCode.SERVER_CONTROLLER:
                    PipeServerController controller = (PipeServerController)message;
                    UpdateController(controller.ProductName, controller.InstanceGuid, controller.ProductGuid, (int)controller.ProductIndex);
                    break;

                case PipeCode.SERVER_SETTINGS:
                    PipeServerSettings settings = (PipeServerSettings)message;
                    UpdateSettings(settings.settings);
                    break;
            }
        }

        private void OnClientDisconnected(object sender)
        {
            UpdateStatus(false);
        }

        private void ControllerHelper_Load(object sender, EventArgs e)
        {
            // initialize GUI
            cB_RunAtStartup.Checked = RunAtStartup = Properties.Settings.Default.RunAtStartup;
            cB_StartMinimized.Checked = StartMinimized = Properties.Settings.Default.StartMinimized;
            cB_CloseMinimizes.Checked = CloseMinimises = Properties.Settings.Default.CloseMinimises;
            cB_touchpad.Checked = HookMouse = Properties.Settings.Default.HookMouse;

            // elevation check

            if (!IsElevated)
            {
                // disable service control
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                // display warning message
                toolTip1.SetToolTip(cB_RunAtStartup, "Run this tool as Administrator to unlock these settings.");
                toolTip1.SetToolTip(gb_SettingsService, "Run this tool as Administrator to unlock these settings.");

                // disable run at startup button
                cB_RunAtStartup.Enabled = false;
            }
            else
            {
                // initialize Service Manager
                ServiceManager = new ServiceManager("ControllerService", this, ServiceName, ServiceDescription, logger);

                // initialize Task Manager
                DefineTask();
            }

            UpdateStatus(false);

            // start pipe client
            PipeClient.Start();

            // initialize Profile Manager
            ProfileManager = new ProfileManager(CurrentPathProfiles, this, logger);

            // start mouse hook
            if (HookMouse) m_Hook.Start();

            // monitor processes
            MonitorTimer = new Timer(1000) { Enabled = true, AutoReset = true };
            MonitorTimer.Elapsed += MonitorHelper;
        }

        public void UpdateProcess(int ProcessId, string ProcessPath)
        {
            try
            {
                string ProcessExec = Path.GetFileName(ProcessPath);

                if (ProfileManager.profiles.ContainsKey(ProcessExec))
                {
                    Profile profile = ProfileManager.profiles[ProcessExec];
                    profile.fullpath = ProcessPath;

                    ProfileManager.UpdateProfile(profile);

                    PipeClient.SendMessage(new PipeClientProfile { profile = profile });

                    logger.LogInformation("Profile {0} applied", profile.name);
                }
                else
                    PipeClient.SendMessage(new PipeClientProfile() { profile = new Profile("default", "") });
            }
            catch (Exception) { }
        }

        private void ControllerHelper_Shown(object sender, EventArgs e)
        {
        }

        private void ControllerHelper_Resize(object sender, EventArgs e)
        {
            if (CurrentWindowState == WindowState)
                return;

            if (WindowState == FormWindowState.Minimized)
            {
                notifyIcon1.Visible = true;
                ShowInTaskbar = false;
            }
            else if (WindowState == FormWindowState.Normal)
            {
                notifyIcon1.Visible = false;
                ShowInTaskbar = true;
            }

            CurrentWindowState = WindowState;
        }

        private void ControllerHelper_Close(object sender, FormClosingEventArgs e)
        {
            if (CloseMinimises && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Normal;
        }

        private void ControllerHelper_Closed(object sender, FormClosedEventArgs e)
        {
            PipeClient.Stop();
            m_Hook.Stop();
        }

        private void MonitorHelper(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // refresh current process
                IntPtr hWnd = GetForegroundWindow();
                IntPtr processId;

                if (GetWindowThreadProcessId(hWnd, out processId) == 0)
                    return;

                if (processId != CurrentProcess)
                {
                    Process proc = Process.GetProcessById((int)processId);
                    string path = Utils.GetPathToApp(proc);

                    UpdateProcess((int)processId, path);

                    CurrentProcess = processId;
                }
            }
        }

        public void UpdateStatus(bool status)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctl in tabDevices.Controls)
                    ctl.Enabled = status;
                gb_SettingsUDP.Enabled = status;
            });
        }

        public void UpdateScreen()
        {
            PipeClient.SendMessage(new PipeClientScreen
            {
                width = Screen.PrimaryScreen.Bounds.Width,
                height = Screen.PrimaryScreen.Bounds.Height
            });
        }

        public void UpdateController(string ProductName, Guid InstanceGuid, Guid ProductGuid, int ProductIndex)
        {
            CurrentController = new Controller(ProductName, InstanceGuid, ProductGuid, ProductIndex);

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                lB_Devices.Items.Clear();
                lB_Devices.Items.Add(CurrentController);

                lB_Devices.SelectedItem = CurrentController;
            });

            logger.LogInformation("{0} connected on port {1}", CurrentController.ProductName, CurrentController.ProductIndex);
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                cB_HidMode.SelectedItem = HIDmodes[args["HIDmode"]];
                cB_HIDcloak.SelectedItem = args["HIDcloaked"];
                cB_uncloak.Checked = bool.Parse(args["HIDuncloakonclose"]);
                cB_gyro.Checked = bool.Parse(args["gyrometer"]);
                cB_accelero.Checked = bool.Parse(args["accelerometer"]);
                tB_PullRate.Value = int.Parse(args["HIDrate"]);
                tB_VibrationStr.Value = int.Parse(args["HIDstrength"]);
                cB_UDPEnable.Checked = bool.Parse(args["DSUEnabled"]);
                tB_UDPIP.Text = args["DSUip"];
                tB_UDPPort.Value = int.Parse(args["DSUport"]);
            });
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #region GUI
        private void lB_Devices_SelectedIndexChanged(object sender, EventArgs e)
        {
            Controller con = (Controller)lB_Devices.SelectedItem;

            if (con == null)
                return;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                tB_InstanceID.Text = $"{con.InstanceGuid}";
                tB_ProductID.Text = $"{con.ProductGuid}";
            });

        }

        private void cB_HIDcloak_SelectedIndexChanged(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeClientSettings
            {
                settings = new Dictionary<string, string>
                {
                    { "HIDcloaked", cB_HIDcloak.Text }
                }
            });
        }

        private void tB_PullRate_Scroll(object sender, EventArgs e)
        {
            // update mouse hook delay based on controller pull rate
            m_Hook.SetInterval(tB_PullRate.Value);

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_PullRate, $"{tB_PullRate.Value} Miliseconds");
            });

            PipeClient.SendMessage(new PipeClientSettings
            {
                settings = new Dictionary<string, string>
                {
                    { "HIDrate", $"{tB_PullRate.Value}" }
                }
            });
        }

        private void tB_VibrationStr_Scroll(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_VibrationStr, $"{tB_VibrationStr.Value}%");
            });

            PipeClient.SendMessage(new PipeClientSettings
            {
                settings = new Dictionary<string, string>
                {
                    { "HIDstrength", $"{tB_VibrationStr.Value}" }
                }
            });
        }

        private void b_UDPApply_Click(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeClientSettings
            {
                settings = new Dictionary<string, string>
                {
                    { "DSUip", $"{tB_UDPIP.Text}" },
                    { "DSUport", $"{tB_UDPPort.Value}" },
                    { "DSUEnabled", $"{cB_UDPEnable.Checked}" }
                }
            });
        }

        private void b_CreateProfile_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var path = openFileDialog1.FileName;
                    var name = openFileDialog1.SafeFileName;

                    Profile profile = new Profile(name, path);

                    ProfileManager.profiles[name] = profile;
                    ProfileManager.SerializeProfile(profile);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
            }
        }

        private void b_DeleteProfile_Click(object sender, EventArgs e)
        {
            Profile profile = (Profile)lB_Profiles.SelectedItem;
            ProfileManager.DeleteProfile(profile);

            lB_Profiles.SelectedIndex = -1;
        }

        private void cB_RunAtStartup_CheckedChanged(object sender, EventArgs e)
        {
            RunAtStartup = cB_RunAtStartup.Checked;
            Properties.Settings.Default.RunAtStartup = RunAtStartup;
            Properties.Settings.Default.Save();

            UpdateTask();

            logger.LogInformation("Controller Helper run at startup set to {0}", RunAtStartup);
        }

        private void DefineTask()
        {
            TaskService TaskServ = new TaskService();
            CurrentTask = TaskServ.FindTask(ServiceName);

            TaskDefinition td = TaskService.Instance.NewTask();
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            td.Settings.Enabled = false;
            td.Triggers.Add(new LogonTrigger());
            td.Actions.Add(new ExecAction(CurrentExe));
            CurrentTask = TaskService.Instance.RootFolder.RegisterTaskDefinition(ServiceName, td);

            logger.LogInformation("Task Scheduler: {0} created", ServiceName);
        }

        public void UpdateTask()
        {
            if (CurrentTask == null)
                return;

            if (RunAtStartup && !CurrentTask.Enabled)
                CurrentTask.Enabled = true;
            else if (!RunAtStartup && CurrentTask.Enabled)
                CurrentTask.Enabled = false;
        }

        private void cB_uncloak_CheckedChanged(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeClientSettings
            {
                settings = new Dictionary<string, string>
                {
                    { "HIDuncloakonclose", $"{cB_uncloak.Checked}" }
                }
            });
        }

        private void cB_touchpad_CheckedChanged(object sender, EventArgs e)
        {
            HookMouse = cB_touchpad.Checked;
            Properties.Settings.Default.HookMouse = HookMouse;
            Properties.Settings.Default.Save();

            if (HookMouse) m_Hook.Start(); else m_Hook.Stop();
        }

        private void cB_StartMinimized_CheckedChanged(object sender, EventArgs e)
        {
            StartMinimized = cB_StartMinimized.Checked;
            Properties.Settings.Default.StartMinimized = StartMinimized;
            Properties.Settings.Default.Save();
        }

        private void cB_CloseMinimizes_CheckedChanged(object sender, EventArgs e)
        {
            CloseMinimises = cB_CloseMinimizes.Checked;
            Properties.Settings.Default.CloseMinimises = CloseMinimises;
            Properties.Settings.Default.Save();
        }

        private void lB_Profiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            Profile profile = (Profile)lB_Profiles.SelectedItem;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                if (profile == null)
                {
                    gB_ProfileDetails.Enabled = false;
                    gB_ProfileOptions.Enabled = false;
                }
                else
                {
                    gB_ProfileDetails.Enabled = true;
                    gB_ProfileOptions.Enabled = true;

                    tB_ProfileName.Text = profile.name;
                    tB_ProfilePath.Text = profile.path;
                    toolTip1.SetToolTip(tB_ProfilePath, profile.error != ProfileErrorCode.None ? $"Can't reach: {profile.path}" : $"{profile.path}");

                    cB_Whitelist.Checked = profile.whitelisted;
                    cB_Wrapper.Checked = profile.use_wrapper;

                    tb_ProfileGyroValue.Value = (int)(profile.gyrometer * 10.0f);
                    tb_ProfileAcceleroValue.Value = (int)(profile.accelerometer * 10.0f);
                }
            });
        }

        private void tb_ProfileGyroValue_Scroll(object sender, EventArgs e)
        {
            Profile profile = (Profile)lB_Profiles.SelectedItem;
            if (profile == null)
                return;

            float value = tb_ProfileGyroValue.Value / 10.0f;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tb_ProfileGyroValue, $"value: {value}");
            });
        }

        private void tb_ProfileAcceleroValue_Scroll(object sender, EventArgs e)
        {
            Profile profile = (Profile)lB_Profiles.SelectedItem;
            if (profile == null)
                return;

            float value = tb_ProfileAcceleroValue.Value / 10.0f;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tb_ProfileAcceleroValue, $"value: {value}");
            });
        }

        private void b_ApplyProfile_Click(object sender, EventArgs e)
        {
            Profile profile = (Profile)lB_Profiles.SelectedItem;
            if (profile == null)
                return;

            float gyro_value = tb_ProfileGyroValue.Value / 10.0f;
            float acce_value = tb_ProfileAcceleroValue.Value / 10.0f;

            profile.gyrometer = gyro_value;
            profile.accelerometer = acce_value;
            profile.whitelisted = cB_Whitelist.Checked;
            profile.use_wrapper = cB_Wrapper.Checked;

            ProfileManager.UpdateProfile(profile);
            ProfileManager.SerializeProfile(profile);
        }

        private void cB_gyro_CheckedChanged(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                cB_gyro.Text = cB_gyro.Checked ? "Gyrometer detected" : "No gyrometer detected";
            });
        }

        private void cB_accelero_CheckedChanged(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                cB_accelero.Text = cB_accelero.Checked ? "Accelerometer detected" : "No accelerometer detected";
            });
        }

        public void UpdateProfileList(Profile profile)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                int idx = lB_Profiles.Items.IndexOf(profile);

                foreach (Profile pr in lB_Profiles.Items)
                    if (pr.path == profile.path)
                    {
                        // IndexOf will always fail !
                        idx = lB_Profiles.Items.IndexOf(pr);
                        break;
                    }

                if (idx == -1)
                    lB_Profiles.Items.Add(profile);
                else
                    lB_Profiles.Items[idx] = profile;

                lB_Profiles.SelectedItem = profile;
            });
        }

        public void DeleteProfile(Profile profile)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                int idx = lB_Profiles.Items.IndexOf(profile);
                if (idx != -1)
                    lB_Profiles.Items.RemoveAt(idx);
            });
        }

        public void UpdateService(ServiceControllerStatus status)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                gb_SettingsService.SuspendLayout();

                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        if (b_ServiceInstall.Enabled == true) b_ServiceInstall.Enabled = false;
                        if (b_ServiceDelete.Enabled == false) b_ServiceDelete.Enabled = true;
                        if (b_ServiceStart.Enabled == false) b_ServiceStart.Enabled = true;
                        if (b_ServiceStop.Enabled == true) b_ServiceStop.Enabled = false;
                        break;
                    case ServiceControllerStatus.Running:
                        if (b_ServiceInstall.Enabled == true) b_ServiceInstall.Enabled = false;
                        if (b_ServiceDelete.Enabled == true) b_ServiceDelete.Enabled = false;
                        if (b_ServiceStart.Enabled == true) b_ServiceStart.Enabled = false;
                        if (b_ServiceStop.Enabled == false) b_ServiceStop.Enabled = true;
                        break;
                    default:
                        if (b_ServiceInstall.Enabled == false) b_ServiceInstall.Enabled = true;
                        if (b_ServiceDelete.Enabled == true) b_ServiceDelete.Enabled = false;
                        if (b_ServiceStart.Enabled == true) b_ServiceStart.Enabled = false;
                        if (b_ServiceStop.Enabled == true) b_ServiceStop.Enabled = false;
                        break;
                }

                gb_SettingsService.ResumeLayout();
            });
        }

        private void b_ServiceInstall_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                ServiceManager.CreateService(CurrentPathService);
            });
        }

        private void b_ServiceDelete_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                ServiceManager.DeleteService();
            });
        }

        private void b_ServiceStart_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                ServiceManager.StartService();
            });
        }

        private void b_ServiceStop_Click(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                ServiceManager.StopService();
            });
        }
        #endregion
    }
}
