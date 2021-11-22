using ControllerService;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

        private Controller CurrentController;

        private MouseHook m_Hook;

        private FormWindowState CurrentWindowState;
        private object updateLock = new();

        private HIDmode HideDS4 = new HIDmode("DualShock4Controller", "DualShock 4 emulation");
        private HIDmode HideXBOX = new HIDmode("Xbox360Controller", "Xbox 360 emulation");
        private Dictionary<string, HIDmode> HIDmodes = new();

        public static string CurrentExe, CurrentPath, CurrentPathProfiles, CurrentPathLogs;

        private bool RunAtStartup, StartMinimized, CloseMinimises, HookMouse;

        public ProfileManager ProfileManager;
        private readonly Logger logger;

        public ControllerHelper()
        {
            InitializeComponent();

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // initialize logger
            logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File($"{CurrentPathLogs}\\ControllerHelper.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

            // initialize pipe client
            PipeClient = new PipeClient("ControllerService", this, logger);

            // initialize mouse hook
            m_Hook = new MouseHook(PipeClient);

            cB_HIDdevice.Items.Add(HideDS4);
            cB_HIDdevice.Items.Add(HideXBOX);

            HIDmodes.Add("DualShock4Controller", HideDS4);
            HIDmodes.Add("Xbox360Controller", HideXBOX);

            // settings
            checkBox3.Checked = RunAtStartup = Properties.Settings.Default.RunAtStartup;
            checkBox4.Checked = StartMinimized = Properties.Settings.Default.StartMinimized;
            checkBox5.Checked = CloseMinimises = Properties.Settings.Default.CloseMinimises;
            checkBox10.Checked = HookMouse = Properties.Settings.Default.HookMouse;

            if (StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }
        }

        private void ControllerHelper_Load(object sender, EventArgs e)
        {
            // start pipe client
            PipeClient.Start();

            // initialize Profile Manager
            ProfileManager = new ProfileManager(CurrentPathProfiles, this, logger);

            // start mouse hook
            if (HookMouse) m_Hook.Start();

            // monitors processes
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
                    Profile CurrentProfile = ProfileManager.profiles[ProcessExec];
                    if (CurrentProfile.path != ProcessPath)
                    {
                        CurrentProfile.path = ProcessPath;
                        CurrentProfile.Serialize();
                    }

                    PipeClient.SendMessage(new PipeMessage
                    {
                        Code = PipeCode.CLIENT_PROFILE,
                        args = new Dictionary<string, string>
                        {
                            { "muted", Convert.ToString(CurrentProfile.whitelisted) },
                            { "gyrometer", Convert.ToString(CurrentProfile.gyrometer) },
                            { "accelerometer", Convert.ToString(CurrentProfile.accelerometer) }
                        }
                    });

                    logger.Information("Profile {0} applied.", CurrentProfile.name);
                }
                else
                {
                    PipeClient.SendMessage(new PipeMessage
                    {
                        Code = PipeCode.CLIENT_PROFILE,
                        args = new Dictionary<string, string> {
                            { "muted", Convert.ToString(false) },
                            { "gyrometer", Convert.ToString(1.0f) },
                            { "accelerometer", Convert.ToString(1.0f) }
                        }
                    });
                }
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

                // refresh service status
            }
        }

        public void UpdateStatus(bool status)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                tabControl1.Enabled = status;
            });
        }

        public void UpdateController(Dictionary<string, string> args)
        {
            CurrentController = new Controller(args["name"], Guid.Parse(args["guid"]), int.Parse(args["index"]));

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                listBoxDevices.Items.Clear();
                listBoxDevices.Items.Add(CurrentController);

                listBoxDevices.SelectedItem = CurrentController;
            });
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                cB_HIDdevice.SelectedItem = HIDmodes[args["HIDmode"]];
                cB_HIDcloak.SelectedItem = args["HIDcloaked"];
                checkBox7.Checked = bool.Parse(args["HIDuncloakonclose"]);

                checkBox1.Checked = bool.Parse(args["gyrometer"]);
                checkBox2.Checked = bool.Parse(args["accelerometer"]);

                tB_HIDrate.Value = int.Parse(args["HIDrate"]);
                label4.Text = $"{tB_HIDrate.Value} Miliseconds";

                checkBox6.Checked = bool.Parse(args["DSUEnabled"]);
                textBox1.Text = args["DSUip"];
                numericUpDown1.Value = int.Parse(args["DSUport"]);
            });
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        #region GUI
        private void listBoxDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            Controller con = (Controller)listBoxDevices.SelectedItem;

            if (con == null)
                return;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                tB_InstanceID.Text = $"{con.guid}";
            });

        }

        private void cB_HIDcloak_SelectedIndexChanged(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_SETTINGS,
                args = new Dictionary<string, string>
                {
                    { "HIDcloaked", cB_HIDcloak.Text }
                }
            });
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                label4.Text = $"{tB_HIDrate.Value} Miliseconds";
            });

            PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_SETTINGS,
                args = new Dictionary<string, string>
                {
                    { "HIDrate", $"{tB_HIDrate.Value}" }
                }
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_SETTINGS,
                args = new Dictionary<string, string>
                {
                    { "DSUip", $"{textBox1.Text}" },
                    { "DSUport", $"{numericUpDown1.Value}" },
                    { "DSUEnabled", $"{checkBox6.Checked}" }
                }
            });
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var path = openFileDialog1.FileName;
                    var name = openFileDialog1.SafeFileName;

                    ProfileManager.profiles[name] = new Profile(name, path);
                    ProfileManager.profiles[name].Serialize();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                }
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {

            RegistryKey rWrite = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            rWrite.SetValue("ControllerHelper", AppDomain.CurrentDomain.BaseDirectory + $"{AppDomain.CurrentDomain.FriendlyName}.exe");

            RunAtStartup = checkBox3.Checked;
            Properties.Settings.Default.RunAtStartup = RunAtStartup;
            Properties.Settings.Default.Save();
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_SETTINGS,
                args = new Dictionary<string, string>
                {
                    { "HIDuncloakonclose", $"{checkBox7.Checked}" }
                }
            });
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            HookMouse = checkBox10.Checked;
            Properties.Settings.Default.HookMouse = HookMouse;
            Properties.Settings.Default.Save();

            if (HookMouse) m_Hook.Start(); else m_Hook.Stop();
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            StartMinimized = checkBox4.Checked;
            Properties.Settings.Default.StartMinimized = StartMinimized;
            Properties.Settings.Default.Save();
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            CloseMinimises = checkBox5.Checked;
            Properties.Settings.Default.CloseMinimises = CloseMinimises;
            Properties.Settings.Default.Save();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Profile profile = (Profile)listBox1.SelectedItem;

            if (profile == null)
                return;

            this.BeginInvoke((MethodInvoker)delegate ()
            {
                textBox2.Text = profile.name;
                textBox3.Text = profile.path;
            });
        }

        public void UpdateProfile(Profile profile)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                int idx = listBox1.Items.IndexOf(profile);
                if (idx == -1)
                    listBox1.Items.Add(profile);
                else
                    listBox1.Items[idx] = profile;
            });
        }

        public void DeleteProfile(Profile profile)
        {
            this.BeginInvoke((MethodInvoker)delegate ()
            {
                int idx = listBox1.Items.IndexOf(profile);
                if (idx != -1)
                    listBox1.Items.RemoveAt(idx);
            });
        }
        #endregion
    }
}
