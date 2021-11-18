using ControllerService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private PipeClient PipeClient;
        private Timer MonitorTimer;
        private IntPtr CurrentProcess;

        private Controller CurrentController;

        private MouseHook m_Hook;

        private FormWindowState CurrentWindowState;
        private object updateLock = new();

        private HIDmode HideDS4 = new HIDmode("DualShock4Controller", "DualShock 4 emulation");
        private HIDmode HideXBOX = new HIDmode("Xbox360Controller", "Xbox 360 emulation");
        private Dictionary<string, HIDmode> HIDmodes = new();

        public ControllerHelper()
        {
            InitializeComponent();

            cB_HIDdevice.Items.Add(HideDS4);
            cB_HIDdevice.Items.Add(HideXBOX);

            HIDmodes.Add("DualShock4Controller", HideDS4);
            HIDmodes.Add("Xbox360Controller", HideXBOX);
        }

        private void ControllerHelper_Load(object sender, EventArgs e)
        {
            // start the pipe client
            PipeClient = new PipeClient("ControllerService", this);
            PipeClient.Start();

            // start mouse hook
            m_Hook = new MouseHook(PipeClient);
            // m_Hook.Start();

            // monitors processes
            MonitorTimer = new Timer(1000) { Enabled = true, AutoReset = true };
            MonitorTimer.Elapsed += MonitorHelper;
        }

        private void ControllerHelper_Shown(object sender, System.EventArgs e)
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
                IntPtr hWnd = GetForegroundWindow();
                IntPtr processId;

                if (GetWindowThreadProcessId(hWnd, out processId) == 0)
                    return;

                if (processId != CurrentProcess)
                {
                    Process proc = Process.GetProcessById((int)processId);
                    string path = Utils.GetPathToApp(proc);

                    PipeClient.SendMessage(new PipeMessage
                    {
                        Code = PipeCode.CLIENT_PROCESS,
                        args = new Dictionary<string, string>
                        {
                            { "processId", processId.ToString() },
                            { "processPath", path }
                        }
                    });

                    CurrentProcess = processId;
                }
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
                checkBox1.Checked = bool.Parse(args["gyrometer"]);
                checkBox2.Checked = bool.Parse(args["accelerometer"]);

                trackBar1.Value = int.Parse(args["HIDrate"]);
                label4.Text = $"{trackBar1.Value} Miliseconds";
            });
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

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
                label4.Text = $"{trackBar1.Value} Miliseconds";
            });

            PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_SETTINGS,
                args = new Dictionary<string, string>
                {
                    { "HIDrate", $"{trackBar1.Value}" }
                }
            });
        }
    }
}
