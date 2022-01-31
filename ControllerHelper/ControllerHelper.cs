using ControllerCommon;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using System.Xml;
using Windows.System.Diagnostics;
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

        public static PipeClient pipeClient;
        public static PipeServer pipeServer;
        public static CmdParser cmdParser;
        public string[] args;

        private Timer MonitorTimer;
        private uint CurrentProcess;

        public static Controller CurrentController;

        private MouseHook m_Hook;
        private ToastManager m_ToastManager;

        private FormWindowState prevWindowState;
        private object updateLock = new();

        public string CurrentExe, CurrentPath, CurrentPathService, CurrentPathProfiles, CurrentPathLogs;

        private bool RunAtStartup, StartMinimized, CloseMinimises, HookMouse;
        private bool IsElevated, FirstStart, appClosing, ToastEnable;

        public ProfileManager ProfileManager;
        public HIDmode HIDmode;

        public ServiceManager ServiceManager;

        // TaskManager vars
        private static Task CurrentTask;

        private readonly ILogger logger;

        public ControllerHelper(string[] Arguments, ILogger logger)
        {
            InitializeComponent();

            this.logger = logger;
            this.args = Arguments;

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(CurrentAssembly.Location);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;

            // paths
            CurrentExe = Process.GetCurrentProcess().MainModule.FileName;
            CurrentPath = AppDomain.CurrentDomain.BaseDirectory;
            CurrentPathProfiles = Path.Combine(CurrentPath, "profiles");
            CurrentPathService = Path.Combine(CurrentPath, "ControllerService.exe");
            CurrentPathLogs = Path.Combine(CurrentPath, "Logs");

            // settings
            IsElevated = Utils.IsAdministrator();
            FirstStart = Properties.Settings.Default.FirstStart;

            // form
            this.Text += $" ({(IsElevated ? strings.Administrator : strings.User)})";
            this.Text += $" ({fileVersionInfo.FileVersion})";

            lb_AboutVersion.Text = String.Format(strings.Build, fileVersionInfo.FileVersion);

            // initialize log
            logger.LogInformation("{0} ({1})", CurrentAssembly.GetName(), fileVersionInfo.FileVersion);

            // verifying HidHide is installed
            if (!File.Exists(CurrentPathService))
            {
                logger.LogCritical("Controller Service executable is missing");
                throw new InvalidOperationException();
            }

            // initialize pipe client
            pipeClient = new PipeClient("ControllerService", logger);
            pipeClient.Connected += OnClientConnected;
            pipeClient.Disconnected += OnClientDisconnected;
            pipeClient.ServerMessage += OnServerMessage;

            // initialize pipe server
            pipeServer = new PipeServer("ControllerHelper", logger);
            pipeServer.ClientMessage += OnClientMessage;

            // initialize Profile Manager
            ProfileManager = new ProfileManager(CurrentPathProfiles, logger, pipeClient);
            ProfileManager.Deleted += ProfileDeleted;
            ProfileManager.Updated += ProfileUpdated;

            // initialize command parser
            cmdParser = new CmdParser(pipeClient, this, logger);

            // initialize mouse hook
            m_Hook = new MouseHook(pipeClient, logger);

            // initialize toast manager
            m_ToastManager = new ToastManager("ControllerService");

            // initialize Service Manager
            ServiceManager = new ServiceManager("ControllerService", strings.ServiceName, strings.ServiceDescription, logger);
            ServiceManager.Updated += UpdateService;

            if (IsElevated)
            {
                // initialize Task Manager
                DefineTask();
                UpdateTask();
            }

            foreach (HIDmode mode in (HIDmode[])Enum.GetValues(typeof(HIDmode)))
                cB_HidMode.Items.Add(Utils.GetDescriptionFromEnumValue(mode));

            foreach (GamepadButtonFlags button in (GamepadButtonFlags[])Enum.GetValues(typeof(GamepadButtonFlags)))
                cB_UMCInputButton.Items.Add(Utils.GetDescriptionFromEnumValue(button));

            // update UI
            cB_RunAtStartup.Checked = RunAtStartup = Properties.Settings.Default.RunAtStartup;
            cB_StartMinimized.Checked = StartMinimized = Properties.Settings.Default.StartMinimized;
            cB_CloseMinimizes.Checked = CloseMinimises = Properties.Settings.Default.CloseMinimises;
            cB_touchpad.Checked = HookMouse = Properties.Settings.Default.HookMouse;
            cB_ToastEnable.Checked = ToastEnable = Properties.Settings.Default.ToastEnable;

            if (FirstStart)
            {
                if (IsElevated)
                {
                    MessageBox.Show(strings.ServiceWelcome, strings.ToastTitle);

                    this.args = new string[] { "service", "--action=install" };

                    FirstStart = false;
                    Properties.Settings.Default.FirstStart = FirstStart;
                    Properties.Settings.Default.Save();
                }
                else
                {
                    m_ToastManager.SendToast(strings.ToastTitle, strings.ToastInitialization);
                }
            }
        }

        private void OnClientConnected(object sender)
        {
            // start mouse hook
            if (HookMouse) m_Hook.Start();

            // send default profile to Service
            pipeClient.SendMessage(new PipeClientProfile() { profile = ProfileManager.GetDefault() });

            // start processes monitor
            MonitorTimer = new Timer(1000) { Enabled = true, AutoReset = true };
            MonitorTimer.Elapsed += MonitorHelper;
        }

        private void OnClientMessage(object sender, PipeMessage e)
        {
            PipeConsoleArgs console = (PipeConsoleArgs)e;

            if (console.args.Length == 0)
                BeginInvoke((MethodInvoker)delegate () { WindowState = prevWindowState; });
            else
                cmdParser.ParseArgs(console.args);

            pipeServer.SendMessage(new PipeShutdown());
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_PING:
                    UpdateStatus(true);
                    UpdateScreen();
                    break;

                case PipeCode.SERVER_TOAST:
                    PipeServerToast toast = (PipeServerToast)message;
                    m_ToastManager.SendToast(toast.title, toast.content, toast.image);
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
            // stop mouse hook
            if (m_Hook.hooked)
                m_Hook.Stop();

            // stop processes monitor
            MonitorTimer.Elapsed -= MonitorHelper;

            UpdateStatus(false);
        }

        private void ControllerHelper_Load(object sender, EventArgs e)
        {
            // update Position and Size
            Size = new Size((int)Math.Max(this.MinimumSize.Width, Properties.Settings.Default.MainWindowWidth), (int)Math.Max(this.MinimumSize.Height, Properties.Settings.Default.MainWindowHeight));
            Location = new Point((int)Math.Max(0, Properties.Settings.Default.MainWindowX), (int)Math.Max(0, Properties.Settings.Default.MainWindowY));
            WindowState = (FormWindowState)Properties.Settings.Default.WindowState;

            // elevation check
            if (!IsElevated)
            {
                // disable service control
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;

                // display warning message
                toolTip1.SetToolTip(cB_RunAtStartup, strings.WarningElevated);
                toolTip1.SetToolTip(gb_SettingsService, strings.WarningElevated);
                toolTip1.SetToolTip(gb_SettingsInterface, strings.WarningElevated);

                b_ApplyProfile.Enabled = false;
                toolTip1.SetToolTip(gB_ProfileDetails, strings.WarningElevated);

                // disable run at startup button
                cB_RunAtStartup.Enabled = false;
                toolTip1.SetToolTip(cB_RunAtStartup, strings.WarningElevated);
            }

            UpdateStatus(false);

            // start Service Manager
            ServiceManager.Start();

            // start pipe client and server
            pipeClient.Start();
            pipeServer.Start();

            // start Profile Manager
            ProfileManager.Start();

            // execute args
            cmdParser.ParseArgs(args);
        }

        public void UpdateProcess(int ProcessId, string ProcessPath, string ProcessName)
        {
            try
            {
                string ProcessExec = Path.GetFileNameWithoutExtension(ProcessPath);

                if (ProfileManager.profiles.ContainsKey(ProcessExec))
                {
                    Profile profile = ProfileManager.profiles[ProcessExec];
                    profile.fullpath = ProcessPath;

                    ProfileManager.UpdateProfile(profile);

                    pipeClient.SendMessage(new PipeClientProfile { profile = profile });

                    logger.LogInformation("Profile {0} applied", profile.name);
                }
                else
                    pipeClient.SendMessage(new PipeClientProfile());
            }
            catch (Exception) { }
        }

        private void ControllerHelper_Resize(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case FormWindowState.Minimized:
                    notifyIcon1.Visible = true;
                    ShowInTaskbar = false;
                    break;
                case FormWindowState.Normal:
                case FormWindowState.Maximized:
                    notifyIcon1.Visible = false;
                    ShowInTaskbar = true;
                    prevWindowState = WindowState;
                    break;
            }
        }

        private void ControllerHelper_Close(object sender, FormClosingEventArgs e)
        {
            // position and size settings
            switch (WindowState)
            {
                case FormWindowState.Normal:
                    Properties.Settings.Default.MainWindowX = (uint)Location.X;
                    Properties.Settings.Default.MainWindowY = (uint)Location.Y;

                    Properties.Settings.Default.MainWindowWidth = (uint)Size.Width;
                    Properties.Settings.Default.MainWindowHeight = (uint)Size.Height;
                    break;
            }
            Properties.Settings.Default.WindowState = (int)WindowState;

            if (CloseMinimises && e.CloseReason == CloseReason.UserClosing && !appClosing)
            {
                e.Cancel = true;
                WindowState = FormWindowState.Minimized;
            }

            Properties.Settings.Default.Save();
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            WindowState = prevWindowState;
        }

        private void ControllerHelper_Closed(object sender, FormClosedEventArgs e)
        {
            ServiceManager.Stop();

            if (pipeClient.connected)
                pipeClient.Stop();

            if (pipeServer.connected)
                pipeServer.Stop();

            ProfileManager.Stop();

            if (m_Hook.hooked)
                m_Hook.Stop();
        }

        private void MonitorHelper(object sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                uint processId;
                string name = string.Empty;
                string exec = string.Empty;
                string path = string.Empty;

                ProcessDiagnosticInfo process = new FindHostedProcess().Process;
                if (process == null)
                    return;

                processId = process.ProcessId;

                if (processId != CurrentProcess)
                {
                    Process proc = Process.GetProcessById((int)processId);
                    path = Utils.GetPathToApp(proc);
                    exec = process.ExecutableFileName;

                    if (process.IsPackaged)
                    {
                        var apps = process.GetAppDiagnosticInfos();
                        if (apps.Count > 0)
                            name = apps.First().AppInfo.DisplayInfo.DisplayName;
                        else
                            name = Path.GetFileNameWithoutExtension(exec);
                    }
                    else
                        name = Path.GetFileNameWithoutExtension(exec);

                    UpdateProcess((int)processId, path, name);

                    CurrentProcess = processId;
                }
            }
        }

        public void UpdateScreen()
        {
            pipeClient.SendMessage(new PipeClientScreen
            {
                width = Screen.PrimaryScreen.Bounds.Width,
                height = Screen.PrimaryScreen.Bounds.Height
            });
        }

        public void ForceExit()
        {
            Application.Exit();
        }

        #region GUI
        public void UpdateStatus(bool status)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctl in tabDevices.Controls)
                    ctl.Enabled = status;
                gb_SettingsUDP.Enabled = status;
            });
        }

        public void UpdateController(string ProductName, Guid InstanceGuid, Guid ProductGuid, int ProductIndex)
        {
            CurrentController = new Controller(ProductName, InstanceGuid, ProductGuid, ProductIndex);

            BeginInvoke((MethodInvoker)delegate ()
            {
                lB_Devices.Items.Clear();
                lB_Devices.Items.Add(CurrentController);

                lB_Devices.SelectedItem = CurrentController;
            });

            logger.LogInformation("{0} connected on port {1}", CurrentController.ProductName, CurrentController.ProductIndex);
        }

        public void UpdateSettings(Dictionary<string, string> args)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (KeyValuePair<string, string> pair in args)
                {
                    string name = pair.Key;
                    string property = pair.Value;

                    switch (name)
                    {
                        case "HIDmode":
                            cB_HidMode.SelectedIndex = int.Parse(args[name]);
                            break;
                        case "HIDcloaked":
                            cB_HIDcloak.Checked = bool.Parse(args[name]);
                            break;
                        case "HIDuncloakonclose":
                            cB_uncloak.Checked = bool.Parse(args[name]);
                            break;
                        case "gyrometer":
                            cB_gyro.Checked = bool.Parse(args[name]);
                            break;
                        case "accelerometer":
                            cB_accelero.Checked = bool.Parse(args[name]);
                            break;
                        case "HIDrate":
                            tB_PullRate.Value = int.Parse(args[name]);
                            break;
                        case "HIDstrength":
                            tB_VibrationStr.Value = int.Parse(args[name]);
                            break;
                        case "DSUEnabled":
                            cB_UDPEnable.Checked = bool.Parse(args[name]);
                            break;
                        case "DSUip":
                            tB_UDPIP.Text = args[name];
                            break;
                        case "DSUport":
                            tB_UDPPort.Value = int.Parse(args[name]);
                            break;
                        case "ToastEnable":
                            m_ToastManager.Enabled = bool.Parse(args[name]);
                            cB_ToastEnable.Checked = bool.Parse(args[name]);
                            break;
                    }
                }
            });
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            appClosing = true;
            this.Close();
        }

        private void lB_Devices_SelectedIndexChanged(object sender, EventArgs e)
        {
            Controller con = (Controller)lB_Devices.SelectedItem;

            if (con == null)
                return;

            BeginInvoke((MethodInvoker)delegate ()
            {
                tB_InstanceID.Text = con.InstanceGuid.ToString();
                tB_ProductID.Text = con.ProductGuid.ToString();
            });

        }

        private void tB_PullRate_Scroll(object sender, EventArgs e)
        {
            // update mouse hook delay based on controller pull rate
            m_Hook.SetInterval(tB_PullRate.Value);

            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_PullRate, $"{tB_PullRate.Value} Miliseconds");
            });

            PipeClientSettings settings = new PipeClientSettings("HIDrate", tB_PullRate.Value);
            pipeClient.SendMessage(settings);
        }

        private void tB_VibrationStr_Scroll(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_VibrationStr, $"{tB_VibrationStr.Value}%");
            });

            PipeClientSettings settings = new PipeClientSettings("HIDstrength", tB_VibrationStr.Value);
            pipeClient.SendMessage(settings);
        }

        private void b_UDPApply_Click(object sender, EventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings();
            settings.settings.Add("DSUip", tB_UDPIP.Text);
            settings.settings.Add("DSUport", tB_UDPPort.Value);
            settings.settings.Add("DSUEnabled", cB_UDPEnable.Checked);

            pipeClient.SendMessage(settings);
        }

        private void b_CreateProfile_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var file = openFileDialog1.SafeFileName;
                    var path = openFileDialog1.FileName;
                    var folder = Path.GetDirectoryName(path);
                    var ext = Path.GetExtension(file);

                    switch (ext)
                    {
                        default:
                        case ".exe":
                            break;
                        case ".xml":
                            try
                            {
                                XmlDocument doc = new XmlDocument();
                                doc.Load(path);

                                XmlNodeList Applications = doc.GetElementsByTagName("Applications");
                                foreach (XmlNode node in Applications)
                                {
                                    foreach (XmlNode child in node.ChildNodes)
                                    {
                                        if (child.Name.Equals("Application"))
                                        {
                                            if (child.Attributes != null)
                                            {
                                                foreach (XmlAttribute attribute in child.Attributes)
                                                {
                                                    switch (attribute.Name)
                                                    {
                                                        case "Executable":
                                                            path = Path.Combine(folder, attribute.InnerText);
                                                            file = Path.GetFileName(path);
                                                            break;
                                                    }
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex.Message, true);
                            }
                            break;
                    }

                    var exe = Path.GetFileNameWithoutExtension(file);

                    Profile profile = new Profile(exe, path);
                    ProfileManager.UpdateProfile(profile);
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

            lB_Profiles.SelectedIndex = 0;
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
            CurrentTask = TaskServ.FindTask(strings.ServiceName);

            TaskDefinition td = TaskService.Instance.NewTask();
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            td.Settings.Enabled = false;
            td.Triggers.Add(new LogonTrigger());
            td.Actions.Add(new ExecAction(CurrentExe));
            CurrentTask = TaskService.Instance.RootFolder.RegisterTaskDefinition(strings.ServiceName, td);
        }

        public void UpdateTask()
        {
            if (CurrentTask == null)
                return;

            CurrentTask.Enabled = RunAtStartup;
        }

        private void cB_uncloak_CheckedChanged(object sender, EventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDuncloakonclose", cB_uncloak.Checked);
            pipeClient.SendMessage(settings);
        }

        private void cB_touchpad_CheckedChanged(object sender, EventArgs e)
        {
            HookMouse = cB_touchpad.Checked;
            Properties.Settings.Default.HookMouse = HookMouse;
            Properties.Settings.Default.Save();

            if (HookMouse && pipeClient.connected)
                m_Hook.Start();
            else if (m_Hook.hooked)
                m_Hook.Stop();
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

        private Profile CurrentProfile;
        private void lB_Profiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            CurrentProfile = (Profile)lB_Profiles.SelectedItem;

            BeginInvoke((MethodInvoker)delegate ()
            {
                if (CurrentProfile == null)
                {
                    gB_ProfileDetails.Enabled = false;
                    gB_ProfileOptions.Enabled = false;
                    gB_6axis.Enabled = false;
                }
                else
                {
                    // disable button if is default profile
                    b_DeleteProfile.Enabled = !CurrentProfile.IsDefault;
                    cB_Whitelist.Enabled = !CurrentProfile.IsDefault;
                    cB_Wrapper.Enabled = !CurrentProfile.IsDefault;

                    // error code specific behavior
                    switch (CurrentProfile.error)
                    {
                        case ProfileErrorCode.None:
                            lb_ErrorCode.Visible = false;
                            break;
                        case ProfileErrorCode.MissingExecutable:
                            lb_ErrorCode.Visible = true;
                            lb_ErrorCode.Text = strings.ErrorCodeMissingExecutable;
                            break;
                        case ProfileErrorCode.MissingPath:
                            cB_Wrapper.Enabled = false;
                            lb_ErrorCode.Visible = true;
                            lb_ErrorCode.Text = strings.ErrorCodeMissingPath;
                            break;
                        case ProfileErrorCode.MissingPermission:
                            cB_Wrapper.Enabled = false;
                            lb_ErrorCode.Visible = true;
                            lb_ErrorCode.Text = strings.ErrorCodeMissingPermission;
                            break;
                    }

                    gB_ProfileDetails.Enabled = true;
                    gB_ProfileOptions.Enabled = true;
                    gB_6axis.Enabled = true;

                    tB_ProfileName.Text = CurrentProfile.name;
                    tB_ProfilePath.Text = CurrentProfile.path;
                    toolTip1.SetToolTip(tB_ProfilePath, CurrentProfile.path);

                    cB_Whitelist.Checked = CurrentProfile.whitelisted;
                    cB_Wrapper.Checked = CurrentProfile.use_wrapper;

                    cB_GyroSteering.SelectedIndex = CurrentProfile.steering;

                    cB_InvertHAxis.Checked = CurrentProfile.inverthorizontal;
                    cB_InvertVAxis.Checked = CurrentProfile.invertvertical;

                    cB_UniversalMC.Checked = CurrentProfile.umc_enabled;
                    cB_UMCInputStyle.SelectedIndex = (int)CurrentProfile.umc_input;
                    tB_UMCSensivity.Value = (int)CurrentProfile.umc_sensivity;
                    tB_UMCIntensity.Value = (int)CurrentProfile.umc_intensity;

                    for (int idx = 0; idx < cB_UMCInputButton.Items.Count; idx++)
                    {
                        string value = (string)cB_UMCInputButton.Items[idx];
                        GamepadButtonFlags button = Utils.GetEnumValueFromDescription<GamepadButtonFlags>(value);
                        bool selected = CurrentProfile.umc_trigger.HasFlag(button);
                        cB_UMCInputButton.SetSelected(idx, selected);
                    }

                    tb_ProfileGyroValue.Value = (int)(CurrentProfile.gyrometer * 10.0f);
                    tb_ProfileAcceleroValue.Value = (int)(CurrentProfile.accelerometer * 10.0f);
                }
            });
        }

        private void tb_ProfileGyroValue_Scroll(object sender, EventArgs e)
        {
            float value = tb_ProfileGyroValue.Value / 10.0f;

            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tb_ProfileGyroValue, $"value: {value}");
            });
        }

        private void tb_ProfileAcceleroValue_Scroll(object sender, EventArgs e)
        {
            float value = tb_ProfileAcceleroValue.Value / 10.0f;

            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tb_ProfileAcceleroValue, $"value: {value}");
            });
        }

        private void b_ApplyProfile_Click(object sender, EventArgs e)
        {
            if (CurrentProfile == null)
                return;

            float gyro_value = tb_ProfileGyroValue.Value / 10.0f;
            float acce_value = tb_ProfileAcceleroValue.Value / 10.0f;

            CurrentProfile.gyrometer = gyro_value;
            CurrentProfile.accelerometer = acce_value;
            CurrentProfile.whitelisted = cB_Whitelist.Checked && cB_Whitelist.Enabled;
            CurrentProfile.use_wrapper = cB_Wrapper.Checked && cB_Wrapper.Enabled;

            CurrentProfile.steering = cB_GyroSteering.SelectedIndex;

            CurrentProfile.inverthorizontal = cB_InvertHAxis.Checked && cB_InvertHAxis.Enabled;
            CurrentProfile.invertvertical = cB_InvertVAxis.Checked && cB_InvertVAxis.Enabled;

            CurrentProfile.umc_enabled = cB_UniversalMC.Checked && cB_UniversalMC.Enabled;
            CurrentProfile.umc_input = (InputStyle)cB_UMCInputStyle.SelectedIndex;
            CurrentProfile.umc_sensivity = tB_UMCSensivity.Value;
            CurrentProfile.umc_intensity = tB_UMCIntensity.Value;

            CurrentProfile.umc_trigger = 0;

            foreach (string item in cB_UMCInputButton.SelectedItems)
            {
                GamepadButtonFlags button = Utils.GetEnumValueFromDescription<GamepadButtonFlags>(item);
                CurrentProfile.umc_trigger |= button;
            }

            ProfileManager.profiles[CurrentProfile.name] = CurrentProfile;
            ProfileManager.UpdateProfile(CurrentProfile);
            ProfileManager.SerializeProfile(CurrentProfile);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ServiceStartMode mode;
            switch (cB_ServiceStartup.SelectedIndex)
            {
                case 0:
                    mode = ServiceStartMode.Automatic;
                    break;
                default:
                case 1:
                    mode = ServiceStartMode.Manual;
                    break;
                case 2:
                    mode = ServiceStartMode.Disabled;
                    break;
            }

            ServiceManager.SetStartType(mode);
        }

        private void cB_UniversalMC_CheckedChanged(object sender, EventArgs e)
        {
            gB_ProfileGyro.Enabled = cB_UniversalMC.Checked;
            cB_Whitelist.Enabled = !cB_UniversalMC.Checked && !CurrentProfile.IsDefault;
        }

        private void tB_UMCSensivity_Scroll(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_UMCSensivity, $"value: {tB_UMCSensivity.Value}");
            });
        }

        private void cB_Whitelist_CheckedChanged(object sender, EventArgs e)
        {
            cB_UniversalMC.Enabled = !cB_Whitelist.Checked;
        }

        private void cB_UMCInputStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            cB_UMCInputButton.Enabled = cB_UMCInputStyle.SelectedIndex != 0;
        }

        private void tB_UMCIntensity_Scroll(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                toolTip1.SetToolTip(tB_UMCIntensity, $"value: {tB_UMCIntensity.Value}");
            });
        }

        private void cB_HidMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            HIDmode = (HIDmode)cB_HidMode.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDmode", HIDmode);
            pipeClient.SendMessage(settings);

            // update UI icon to match HIDmode
            BeginInvoke((MethodInvoker)delegate ()
            {
                Bitmap myImage = new(1, 1);

                switch (HIDmode)
                {
                    case HIDmode.DualShock4Controller:
                        myImage = Properties.Resources.HIDmode1;
                        break;
                    case HIDmode.Xbox360Controller:
                        myImage = Properties.Resources.HIDmode2;
                        break;
                    case HIDmode.None:
                        break;
                }

                // Update image next to dropdown selection
                this.pB_HidMode.BackgroundImage = myImage;

                UpdateIcon();
            });
        }

        public void UpdateCloak(bool cloak)
        {
            if (!pipeClient.connected)
                return;

            BeginInvoke((MethodInvoker)delegate ()
            {
                cB_HIDcloak.Checked = cloak;
            });
        }

        public void UpdateHID(HIDmode mode)
        {
            if (!pipeClient.connected)
                return;

            BeginInvoke((MethodInvoker)delegate ()
            {
                cB_HidMode.SelectedIndex = (int)mode;
            });
        }

        private void IL_AboutSource_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenUrl("https://github.com/Valkirie/ControllerService");
        }

        private void IL_AboutWiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenUrl("https://github.com/Valkirie/ControllerService/wiki");
        }

        private void IL_AboutDonate_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenUrl("https://www.paypal.com/paypalme/BenjaminLSR");
        }

        private void cB_ToastEnable_CheckedChanged(object sender, EventArgs e)
        {
            ToastEnable = cB_ToastEnable.Checked;
            Properties.Settings.Default.ToastEnable = ToastEnable;
            Properties.Settings.Default.Save();

            m_ToastManager.Enabled = cB_ToastEnable.Checked;
        }

        private void cB_HIDcloak_CheckedChanged(object sender, EventArgs e)
        {
            PipeClientSettings settings = new PipeClientSettings("HIDcloaked", cB_HIDcloak.Checked);
            pipeClient.SendMessage(settings);
        }

        public void ProfileUpdated(Profile profile)
        {
            // inform Service we have a new default profile
            if (profile.IsDefault)
                pipeClient.SendMessage(new PipeClientProfile() { profile = profile });

            BeginInvoke((MethodInvoker)delegate ()
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

        public void ProfileDeleted(Profile profile)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                int idx = lB_Profiles.Items.IndexOf(profile);
                if (idx != -1)
                    lB_Profiles.Items.RemoveAt(idx);
            });
        }

        public void UpdateService(ServiceControllerStatus status, ServiceStartMode starttype)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {

                UpdateIcon();

                gb_SettingsService.SuspendLayout();

                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        if (b_ServiceInstall.Enabled == true) b_ServiceInstall.Enabled = false;
                        if (b_ServiceDelete.Enabled == false) b_ServiceDelete.Enabled = true;
                        if (b_ServiceStart.Enabled == false) b_ServiceStart.Enabled = true;
                        if (b_ServiceStop.Enabled == true) b_ServiceStop.Enabled = false;
                        if (cB_ServiceStartup.Enabled == false) cB_ServiceStartup.Enabled = true;
                        break;
                    case ServiceControllerStatus.Running:
                        if (b_ServiceInstall.Enabled == true) b_ServiceInstall.Enabled = false;
                        if (b_ServiceDelete.Enabled == true) b_ServiceDelete.Enabled = false;
                        if (b_ServiceStart.Enabled == true) b_ServiceStart.Enabled = false;
                        if (b_ServiceStop.Enabled == false) b_ServiceStop.Enabled = true;
                        if (cB_ServiceStartup.Enabled == false) cB_ServiceStartup.Enabled = true;
                        break;
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        if (b_ServiceInstall.Enabled == true) b_ServiceInstall.Enabled = false;
                        if (b_ServiceDelete.Enabled == true) b_ServiceDelete.Enabled = false;
                        if (b_ServiceStart.Enabled == true) b_ServiceStart.Enabled = false;
                        if (b_ServiceStop.Enabled == false) b_ServiceStop.Enabled = false;
                        if (cB_ServiceStartup.Enabled == false) cB_ServiceStartup.Enabled = false;
                        break;
                    default:
                        if (b_ServiceInstall.Enabled == false) b_ServiceInstall.Enabled = IsElevated;
                        if (b_ServiceDelete.Enabled == true) b_ServiceDelete.Enabled = false;
                        if (b_ServiceStart.Enabled == true) b_ServiceStart.Enabled = false;
                        if (b_ServiceStop.Enabled == true) b_ServiceStop.Enabled = false;
                        if (cB_ServiceStartup.Enabled == true) cB_ServiceStartup.Enabled = false;
                        break;
                }

                switch (starttype)
                {
                    case ServiceStartMode.Disabled:
                        cB_ServiceStartup.SelectedIndex = 2;
                        break;
                    case ServiceStartMode.Automatic:
                        cB_ServiceStartup.SelectedIndex = 0;
                        break;
                    default:
                    case ServiceStartMode.Manual:
                        cB_ServiceStartup.SelectedIndex = 1;
                        break;
                }

                gb_SettingsService.ResumeLayout();

            });
        }

        private void UpdateIcon()
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                Icon myIcon = Properties.Resources.LogoApplicationIcon;

                switch (HIDmode)
                {
                    case HIDmode.None:
                        myIcon = ServiceManager.status == ServiceControllerStatus.Running ? Properties.Resources.HID0StatusIconOn : Properties.Resources.HID0StatusIconOff;
                        break;
                    case HIDmode.DualShock4Controller:
                        myIcon = ServiceManager.status == ServiceControllerStatus.Running ? Properties.Resources.HID1StatusIconOn : Properties.Resources.HID1StatusIconOff;
                        break;
                    case HIDmode.Xbox360Controller:
                        myIcon = ServiceManager.status == ServiceControllerStatus.Running ? Properties.Resources.HID2StatusIconOn : Properties.Resources.HID2StatusIconOff;
                        break;
                }

                // Application icon, indicates controller and service status
                this.Icon = myIcon;
                // Tray icon, indicates controller and service status
                this.notifyIcon1.Icon = myIcon;
            });
        }


        private void b_ServiceInstall_Click(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;
            });
            ServiceManager.CreateService(CurrentPathService);
        }

        private void b_ServiceDelete_Click(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;
            });
            ServiceManager.DeleteService();
        }

        private void b_ServiceStart_Click(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;
            });
            ServiceManager.StartService();
        }

        private void b_ServiceStop_Click(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                foreach (Control ctrl in gb_SettingsService.Controls)
                    ctrl.Enabled = false;
            });
            ServiceManager.StopService();
        }
        #endregion
    }
}
