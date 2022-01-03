
using System;

namespace ControllerHelper
{
    partial class ControllerHelper
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ControllerHelper));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.quitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabDevices = new System.Windows.Forms.TabPage();
            this.gB_DeviceDetails = new System.Windows.Forms.GroupBox();
            this.cB_touchpad = new System.Windows.Forms.CheckBox();
            this.lb_touchpad = new System.Windows.Forms.Label();
            this.lb_gyro = new System.Windows.Forms.Label();
            this.cB_accelero = new System.Windows.Forms.CheckBox();
            this.cB_gyro = new System.Windows.Forms.CheckBox();
            this.lb_accelero = new System.Windows.Forms.Label();
            this.gB_HIDDetails = new System.Windows.Forms.GroupBox();
            this.pB_HidMode = new System.Windows.Forms.PictureBox();
            this.tB_VibrationStr = new System.Windows.Forms.TrackBar();
            this.lb_VibrationStr = new System.Windows.Forms.Label();
            this.tB_PullRate = new System.Windows.Forms.TrackBar();
            this.lb_PullRate = new System.Windows.Forms.Label();
            this.lb_HidMode = new System.Windows.Forms.Label();
            this.cB_HidMode = new System.Windows.Forms.ComboBox();
            this.gB_XinputDetails = new System.Windows.Forms.GroupBox();
            this.cB_HIDcloak = new System.Windows.Forms.CheckBox();
            this.tB_ProductID = new System.Windows.Forms.TextBox();
            this.lb_ProductID = new System.Windows.Forms.Label();
            this.cB_uncloak = new System.Windows.Forms.CheckBox();
            this.lb_HidCloak = new System.Windows.Forms.Label();
            this.tB_InstanceID = new System.Windows.Forms.TextBox();
            this.lb_InstanceID = new System.Windows.Forms.Label();
            this.gB_XinputDevices = new System.Windows.Forms.GroupBox();
            this.lB_Devices = new System.Windows.Forms.ListBox();
            this.tabProfiles = new System.Windows.Forms.TabPage();
            this.gB_ProfileOptions = new System.Windows.Forms.GroupBox();
            this.cB_UniversalMC = new System.Windows.Forms.CheckBox();
            this.lb_Wrapper = new System.Windows.Forms.Label();
            this.lb_UniversalMC = new System.Windows.Forms.Label();
            this.lb_Whitelist = new System.Windows.Forms.Label();
            this.cB_Wrapper = new System.Windows.Forms.CheckBox();
            this.cB_Whitelist = new System.Windows.Forms.CheckBox();
            this.gB_6axis = new System.Windows.Forms.GroupBox();
            this.cB_InvertVAxis = new System.Windows.Forms.CheckBox();
            this.cB_InvertHAxis = new System.Windows.Forms.CheckBox();
            this.lB_InvertVAxis = new System.Windows.Forms.Label();
            this.lB_InvertHAxis = new System.Windows.Forms.Label();
            this.lB_GyroSteering = new System.Windows.Forms.Label();
            this.cB_GyroSteering = new System.Windows.Forms.ComboBox();
            this.tb_ProfileAcceleroValue = new System.Windows.Forms.TrackBar();
            this.lb_ProfileGyro = new System.Windows.Forms.Label();
            this.lb_ProfileAccelero = new System.Windows.Forms.Label();
            this.tb_ProfileGyroValue = new System.Windows.Forms.TrackBar();
            this.gB_ProfileDetails = new System.Windows.Forms.GroupBox();
            this.lb_ErrorCode = new System.Windows.Forms.Label();
            this.b_ApplyProfile = new System.Windows.Forms.Button();
            this.b_DeleteProfile = new System.Windows.Forms.Button();
            this.tB_ProfilePath = new System.Windows.Forms.TextBox();
            this.lb_ProfilePath = new System.Windows.Forms.Label();
            this.tB_ProfileName = new System.Windows.Forms.TextBox();
            this.lb_ProfileName = new System.Windows.Forms.Label();
            this.gB_Profiles = new System.Windows.Forms.GroupBox();
            this.b_CreateProfile = new System.Windows.Forms.Button();
            this.lB_Profiles = new System.Windows.Forms.ListBox();
            this.gB_ProfileGyro = new System.Windows.Forms.GroupBox();
            this.tB_UMCIntensity = new System.Windows.Forms.TrackBar();
            this.cB_UMCInputButton = new System.Windows.Forms.ListBox();
            this.lb_UMCInputButton = new System.Windows.Forms.Label();
            this.lb_UMCIntensity = new System.Windows.Forms.Label();
            this.tB_UMCSensivity = new System.Windows.Forms.TrackBar();
            this.lb_UMCSensivity = new System.Windows.Forms.Label();
            this.lb_UMCInputStyle = new System.Windows.Forms.Label();
            this.cB_UMCInputStyle = new System.Windows.Forms.ComboBox();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.gb_SettingsUDP = new System.Windows.Forms.GroupBox();
            this.b_UDPApply = new System.Windows.Forms.Button();
            this.tB_UDPPort = new System.Windows.Forms.NumericUpDown();
            this.lb_UDPport = new System.Windows.Forms.Label();
            this.tB_UDPIP = new System.Windows.Forms.TextBox();
            this.cB_UDPEnable = new System.Windows.Forms.CheckBox();
            this.gb_SettingsService = new System.Windows.Forms.GroupBox();
            this.lB_ServiceStartup = new System.Windows.Forms.Label();
            this.lB_ServiceStatus = new System.Windows.Forms.Label();
            this.cB_ServiceStartup = new System.Windows.Forms.ComboBox();
            this.b_ServiceStop = new System.Windows.Forms.Button();
            this.b_ServiceStart = new System.Windows.Forms.Button();
            this.b_ServiceDelete = new System.Windows.Forms.Button();
            this.b_ServiceInstall = new System.Windows.Forms.Button();
            this.gb_SettingsInterface = new System.Windows.Forms.GroupBox();
            this.cB_RunAtStartup = new System.Windows.Forms.CheckBox();
            this.cB_CloseMinimizes = new System.Windows.Forms.CheckBox();
            this.cB_StartMinimized = new System.Windows.Forms.CheckBox();
            this.tabAbout = new System.Windows.Forms.TabPage();
            this.lL_AboutDonate = new System.Windows.Forms.LinkLabel();
            this.lL_AboutWiki = new System.Windows.Forms.LinkLabel();
            this.lL_AboutSource = new System.Windows.Forms.LinkLabel();
            this.lb_AboutDescription = new System.Windows.Forms.Label();
            this.lb_AboutAuthor = new System.Windows.Forms.Label();
            this.lb_AboutVersion = new System.Windows.Forms.Label();
            this.lb_AboutTitle = new System.Windows.Forms.Label();
            this.pB_About = new System.Windows.Forms.PictureBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabDevices.SuspendLayout();
            this.gB_DeviceDetails.SuspendLayout();
            this.gB_HIDDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pB_HidMode)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_VibrationStr)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_PullRate)).BeginInit();
            this.gB_XinputDetails.SuspendLayout();
            this.gB_XinputDevices.SuspendLayout();
            this.tabProfiles.SuspendLayout();
            this.gB_ProfileOptions.SuspendLayout();
            this.gB_6axis.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileAcceleroValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileGyroValue)).BeginInit();
            this.gB_ProfileDetails.SuspendLayout();
            this.gB_Profiles.SuspendLayout();
            this.gB_ProfileGyro.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UMCIntensity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UMCSensivity)).BeginInit();
            this.tabSettings.SuspendLayout();
            this.gb_SettingsUDP.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UDPPort)).BeginInit();
            this.gb_SettingsService.SuspendLayout();
            this.gb_SettingsInterface.SuspendLayout();
            this.tabAbout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pB_About)).BeginInit();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            resources.ApplyResources(this.notifyIcon1, "notifyIcon1");
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.notifyIcon1_DoubleClick);
            // 
            // contextMenuStrip1
            // 
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quitToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.toolTip1.SetToolTip(this.contextMenuStrip1, resources.GetString("contextMenuStrip1.ToolTip"));
            // 
            // quitToolStripMenuItem
            // 
            resources.ApplyResources(this.quitToolStripMenuItem, "quitToolStripMenuItem");
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.quitToolStripMenuItem_Click);
            // 
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabDevices);
            this.tabControl1.Controls.Add(this.tabProfiles);
            this.tabControl1.Controls.Add(this.tabSettings);
            this.tabControl1.Controls.Add(this.tabAbout);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.toolTip1.SetToolTip(this.tabControl1, resources.GetString("tabControl1.ToolTip"));
            // 
            // tabDevices
            // 
            resources.ApplyResources(this.tabDevices, "tabDevices");
            this.tabDevices.Controls.Add(this.gB_DeviceDetails);
            this.tabDevices.Controls.Add(this.gB_HIDDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDevices);
            this.tabDevices.Name = "tabDevices";
            this.toolTip1.SetToolTip(this.tabDevices, resources.GetString("tabDevices.ToolTip"));
            this.tabDevices.UseVisualStyleBackColor = true;
            // 
            // gB_DeviceDetails
            // 
            resources.ApplyResources(this.gB_DeviceDetails, "gB_DeviceDetails");
            this.gB_DeviceDetails.Controls.Add(this.cB_touchpad);
            this.gB_DeviceDetails.Controls.Add(this.lb_touchpad);
            this.gB_DeviceDetails.Controls.Add(this.lb_gyro);
            this.gB_DeviceDetails.Controls.Add(this.cB_accelero);
            this.gB_DeviceDetails.Controls.Add(this.cB_gyro);
            this.gB_DeviceDetails.Controls.Add(this.lb_accelero);
            this.gB_DeviceDetails.Name = "gB_DeviceDetails";
            this.gB_DeviceDetails.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_DeviceDetails, resources.GetString("gB_DeviceDetails.ToolTip"));
            // 
            // cB_touchpad
            // 
            resources.ApplyResources(this.cB_touchpad, "cB_touchpad");
            this.cB_touchpad.Name = "cB_touchpad";
            this.toolTip1.SetToolTip(this.cB_touchpad, resources.GetString("cB_touchpad.ToolTip"));
            this.cB_touchpad.UseVisualStyleBackColor = true;
            this.cB_touchpad.CheckedChanged += new System.EventHandler(this.cB_touchpad_CheckedChanged);
            // 
            // lb_touchpad
            // 
            resources.ApplyResources(this.lb_touchpad, "lb_touchpad");
            this.lb_touchpad.Name = "lb_touchpad";
            this.toolTip1.SetToolTip(this.lb_touchpad, resources.GetString("lb_touchpad.ToolTip"));
            // 
            // lb_gyro
            // 
            resources.ApplyResources(this.lb_gyro, "lb_gyro");
            this.lb_gyro.Name = "lb_gyro";
            this.toolTip1.SetToolTip(this.lb_gyro, resources.GetString("lb_gyro.ToolTip"));
            // 
            // cB_accelero
            // 
            resources.ApplyResources(this.cB_accelero, "cB_accelero");
            this.cB_accelero.Name = "cB_accelero";
            this.toolTip1.SetToolTip(this.cB_accelero, resources.GetString("cB_accelero.ToolTip"));
            this.cB_accelero.UseVisualStyleBackColor = true;
            // 
            // cB_gyro
            // 
            resources.ApplyResources(this.cB_gyro, "cB_gyro");
            this.cB_gyro.Name = "cB_gyro";
            this.toolTip1.SetToolTip(this.cB_gyro, resources.GetString("cB_gyro.ToolTip"));
            this.cB_gyro.UseVisualStyleBackColor = true;
            // 
            // lb_accelero
            // 
            resources.ApplyResources(this.lb_accelero, "lb_accelero");
            this.lb_accelero.Name = "lb_accelero";
            this.toolTip1.SetToolTip(this.lb_accelero, resources.GetString("lb_accelero.ToolTip"));
            // 
            // gB_HIDDetails
            // 
            resources.ApplyResources(this.gB_HIDDetails, "gB_HIDDetails");
            this.gB_HIDDetails.Controls.Add(this.pB_HidMode);
            this.gB_HIDDetails.Controls.Add(this.tB_VibrationStr);
            this.gB_HIDDetails.Controls.Add(this.lb_VibrationStr);
            this.gB_HIDDetails.Controls.Add(this.tB_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_HidMode);
            this.gB_HIDDetails.Controls.Add(this.cB_HidMode);
            this.gB_HIDDetails.Name = "gB_HIDDetails";
            this.gB_HIDDetails.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_HIDDetails, resources.GetString("gB_HIDDetails.ToolTip"));
            // 
            // pB_HidMode
            // 
            resources.ApplyResources(this.pB_HidMode, "pB_HidMode");
            this.pB_HidMode.Name = "pB_HidMode";
            this.pB_HidMode.TabStop = false;
            this.toolTip1.SetToolTip(this.pB_HidMode, resources.GetString("pB_HidMode.ToolTip"));
            // 
            // tB_VibrationStr
            // 
            resources.ApplyResources(this.tB_VibrationStr, "tB_VibrationStr");
            this.tB_VibrationStr.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_VibrationStr.LargeChange = 10;
            this.tB_VibrationStr.Maximum = 100;
            this.tB_VibrationStr.Name = "tB_VibrationStr";
            this.tB_VibrationStr.SmallChange = 2;
            this.tB_VibrationStr.TickFrequency = 10;
            this.toolTip1.SetToolTip(this.tB_VibrationStr, resources.GetString("tB_VibrationStr.ToolTip"));
            this.tB_VibrationStr.Value = 50;
            this.tB_VibrationStr.ValueChanged += new System.EventHandler(this.tB_VibrationStr_Scroll);
            // 
            // lb_VibrationStr
            // 
            resources.ApplyResources(this.lb_VibrationStr, "lb_VibrationStr");
            this.lb_VibrationStr.Name = "lb_VibrationStr";
            this.toolTip1.SetToolTip(this.lb_VibrationStr, resources.GetString("lb_VibrationStr.ToolTip"));
            // 
            // tB_PullRate
            // 
            resources.ApplyResources(this.tB_PullRate, "tB_PullRate");
            this.tB_PullRate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_PullRate.Maximum = 150;
            this.tB_PullRate.Minimum = 5;
            this.tB_PullRate.Name = "tB_PullRate";
            this.tB_PullRate.SmallChange = 5;
            this.tB_PullRate.TickFrequency = 5;
            this.toolTip1.SetToolTip(this.tB_PullRate, resources.GetString("tB_PullRate.ToolTip"));
            this.tB_PullRate.Value = 10;
            this.tB_PullRate.ValueChanged += new System.EventHandler(this.tB_PullRate_Scroll);
            // 
            // lb_PullRate
            // 
            resources.ApplyResources(this.lb_PullRate, "lb_PullRate");
            this.lb_PullRate.Name = "lb_PullRate";
            this.toolTip1.SetToolTip(this.lb_PullRate, resources.GetString("lb_PullRate.ToolTip"));
            // 
            // lb_HidMode
            // 
            resources.ApplyResources(this.lb_HidMode, "lb_HidMode");
            this.lb_HidMode.Name = "lb_HidMode";
            this.toolTip1.SetToolTip(this.lb_HidMode, resources.GetString("lb_HidMode.ToolTip"));
            // 
            // cB_HidMode
            // 
            resources.ApplyResources(this.cB_HidMode, "cB_HidMode");
            this.cB_HidMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HidMode.FormattingEnabled = true;
            this.cB_HidMode.Name = "cB_HidMode";
            this.toolTip1.SetToolTip(this.cB_HidMode, resources.GetString("cB_HidMode.ToolTip"));
            this.cB_HidMode.SelectedIndexChanged += new System.EventHandler(this.cB_HidMode_SelectedIndexChanged);
            // 
            // gB_XinputDetails
            // 
            resources.ApplyResources(this.gB_XinputDetails, "gB_XinputDetails");
            this.gB_XinputDetails.Controls.Add(this.cB_HIDcloak);
            this.gB_XinputDetails.Controls.Add(this.tB_ProductID);
            this.gB_XinputDetails.Controls.Add(this.lb_ProductID);
            this.gB_XinputDetails.Controls.Add(this.cB_uncloak);
            this.gB_XinputDetails.Controls.Add(this.lb_HidCloak);
            this.gB_XinputDetails.Controls.Add(this.tB_InstanceID);
            this.gB_XinputDetails.Controls.Add(this.lb_InstanceID);
            this.gB_XinputDetails.Name = "gB_XinputDetails";
            this.gB_XinputDetails.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_XinputDetails, resources.GetString("gB_XinputDetails.ToolTip"));
            // 
            // cB_HIDcloak
            // 
            resources.ApplyResources(this.cB_HIDcloak, "cB_HIDcloak");
            this.cB_HIDcloak.Name = "cB_HIDcloak";
            this.toolTip1.SetToolTip(this.cB_HIDcloak, resources.GetString("cB_HIDcloak.ToolTip"));
            this.cB_HIDcloak.UseVisualStyleBackColor = true;
            this.cB_HIDcloak.CheckedChanged += new System.EventHandler(this.cB_HIDcloak_CheckedChanged);
            // 
            // tB_ProductID
            // 
            resources.ApplyResources(this.tB_ProductID, "tB_ProductID");
            this.tB_ProductID.Name = "tB_ProductID";
            this.tB_ProductID.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tB_ProductID, resources.GetString("tB_ProductID.ToolTip"));
            // 
            // lb_ProductID
            // 
            resources.ApplyResources(this.lb_ProductID, "lb_ProductID");
            this.lb_ProductID.Name = "lb_ProductID";
            this.toolTip1.SetToolTip(this.lb_ProductID, resources.GetString("lb_ProductID.ToolTip"));
            // 
            // cB_uncloak
            // 
            resources.ApplyResources(this.cB_uncloak, "cB_uncloak");
            this.cB_uncloak.Name = "cB_uncloak";
            this.toolTip1.SetToolTip(this.cB_uncloak, resources.GetString("cB_uncloak.ToolTip"));
            this.cB_uncloak.UseVisualStyleBackColor = true;
            this.cB_uncloak.CheckedChanged += new System.EventHandler(this.cB_uncloak_CheckedChanged);
            // 
            // lb_HidCloak
            // 
            resources.ApplyResources(this.lb_HidCloak, "lb_HidCloak");
            this.lb_HidCloak.Name = "lb_HidCloak";
            this.toolTip1.SetToolTip(this.lb_HidCloak, resources.GetString("lb_HidCloak.ToolTip"));
            // 
            // tB_InstanceID
            // 
            resources.ApplyResources(this.tB_InstanceID, "tB_InstanceID");
            this.tB_InstanceID.Name = "tB_InstanceID";
            this.tB_InstanceID.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tB_InstanceID, resources.GetString("tB_InstanceID.ToolTip"));
            // 
            // lb_InstanceID
            // 
            resources.ApplyResources(this.lb_InstanceID, "lb_InstanceID");
            this.lb_InstanceID.Name = "lb_InstanceID";
            this.toolTip1.SetToolTip(this.lb_InstanceID, resources.GetString("lb_InstanceID.ToolTip"));
            // 
            // gB_XinputDevices
            // 
            resources.ApplyResources(this.gB_XinputDevices, "gB_XinputDevices");
            this.gB_XinputDevices.Controls.Add(this.lB_Devices);
            this.gB_XinputDevices.Name = "gB_XinputDevices";
            this.gB_XinputDevices.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_XinputDevices, resources.GetString("gB_XinputDevices.ToolTip"));
            // 
            // lB_Devices
            // 
            resources.ApplyResources(this.lB_Devices, "lB_Devices");
            this.lB_Devices.FormattingEnabled = true;
            this.lB_Devices.Name = "lB_Devices";
            this.toolTip1.SetToolTip(this.lB_Devices, resources.GetString("lB_Devices.ToolTip"));
            this.lB_Devices.SelectedIndexChanged += new System.EventHandler(this.lB_Devices_SelectedIndexChanged);
            // 
            // tabProfiles
            // 
            resources.ApplyResources(this.tabProfiles, "tabProfiles");
            this.tabProfiles.Controls.Add(this.gB_ProfileOptions);
            this.tabProfiles.Controls.Add(this.gB_6axis);
            this.tabProfiles.Controls.Add(this.gB_ProfileDetails);
            this.tabProfiles.Controls.Add(this.gB_Profiles);
            this.tabProfiles.Controls.Add(this.gB_ProfileGyro);
            this.tabProfiles.Name = "tabProfiles";
            this.toolTip1.SetToolTip(this.tabProfiles, resources.GetString("tabProfiles.ToolTip"));
            this.tabProfiles.UseVisualStyleBackColor = true;
            // 
            // gB_ProfileOptions
            // 
            resources.ApplyResources(this.gB_ProfileOptions, "gB_ProfileOptions");
            this.gB_ProfileOptions.Controls.Add(this.cB_UniversalMC);
            this.gB_ProfileOptions.Controls.Add(this.lb_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.lb_UniversalMC);
            this.gB_ProfileOptions.Controls.Add(this.lb_Whitelist);
            this.gB_ProfileOptions.Controls.Add(this.cB_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.cB_Whitelist);
            this.gB_ProfileOptions.Name = "gB_ProfileOptions";
            this.gB_ProfileOptions.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_ProfileOptions, resources.GetString("gB_ProfileOptions.ToolTip"));
            // 
            // cB_UniversalMC
            // 
            resources.ApplyResources(this.cB_UniversalMC, "cB_UniversalMC");
            this.cB_UniversalMC.Name = "cB_UniversalMC";
            this.toolTip1.SetToolTip(this.cB_UniversalMC, resources.GetString("cB_UniversalMC.ToolTip"));
            this.cB_UniversalMC.UseVisualStyleBackColor = true;
            this.cB_UniversalMC.CheckedChanged += new System.EventHandler(this.cB_UniversalMC_CheckedChanged);
            // 
            // lb_Wrapper
            // 
            resources.ApplyResources(this.lb_Wrapper, "lb_Wrapper");
            this.lb_Wrapper.Name = "lb_Wrapper";
            this.toolTip1.SetToolTip(this.lb_Wrapper, resources.GetString("lb_Wrapper.ToolTip"));
            // 
            // lb_UniversalMC
            // 
            resources.ApplyResources(this.lb_UniversalMC, "lb_UniversalMC");
            this.lb_UniversalMC.Name = "lb_UniversalMC";
            this.toolTip1.SetToolTip(this.lb_UniversalMC, resources.GetString("lb_UniversalMC.ToolTip"));
            // 
            // lb_Whitelist
            // 
            resources.ApplyResources(this.lb_Whitelist, "lb_Whitelist");
            this.lb_Whitelist.Name = "lb_Whitelist";
            this.toolTip1.SetToolTip(this.lb_Whitelist, resources.GetString("lb_Whitelist.ToolTip"));
            // 
            // cB_Wrapper
            // 
            resources.ApplyResources(this.cB_Wrapper, "cB_Wrapper");
            this.cB_Wrapper.Name = "cB_Wrapper";
            this.toolTip1.SetToolTip(this.cB_Wrapper, resources.GetString("cB_Wrapper.ToolTip"));
            this.cB_Wrapper.UseVisualStyleBackColor = true;
            // 
            // cB_Whitelist
            // 
            resources.ApplyResources(this.cB_Whitelist, "cB_Whitelist");
            this.cB_Whitelist.Name = "cB_Whitelist";
            this.toolTip1.SetToolTip(this.cB_Whitelist, resources.GetString("cB_Whitelist.ToolTip"));
            this.cB_Whitelist.UseVisualStyleBackColor = true;
            this.cB_Whitelist.CheckedChanged += new System.EventHandler(this.cB_Whitelist_CheckedChanged);
            // 
            // gB_6axis
            // 
            resources.ApplyResources(this.gB_6axis, "gB_6axis");
            this.gB_6axis.Controls.Add(this.cB_InvertVAxis);
            this.gB_6axis.Controls.Add(this.cB_InvertHAxis);
            this.gB_6axis.Controls.Add(this.lB_InvertVAxis);
            this.gB_6axis.Controls.Add(this.lB_InvertHAxis);
            this.gB_6axis.Controls.Add(this.lB_GyroSteering);
            this.gB_6axis.Controls.Add(this.cB_GyroSteering);
            this.gB_6axis.Controls.Add(this.tb_ProfileAcceleroValue);
            this.gB_6axis.Controls.Add(this.lb_ProfileGyro);
            this.gB_6axis.Controls.Add(this.lb_ProfileAccelero);
            this.gB_6axis.Controls.Add(this.tb_ProfileGyroValue);
            this.gB_6axis.Name = "gB_6axis";
            this.gB_6axis.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_6axis, resources.GetString("gB_6axis.ToolTip"));
            // 
            // cB_InvertVAxis
            // 
            resources.ApplyResources(this.cB_InvertVAxis, "cB_InvertVAxis");
            this.cB_InvertVAxis.Name = "cB_InvertVAxis";
            this.toolTip1.SetToolTip(this.cB_InvertVAxis, resources.GetString("cB_InvertVAxis.ToolTip"));
            this.cB_InvertVAxis.UseVisualStyleBackColor = true;
            // 
            // cB_InvertHAxis
            // 
            resources.ApplyResources(this.cB_InvertHAxis, "cB_InvertHAxis");
            this.cB_InvertHAxis.Name = "cB_InvertHAxis";
            this.toolTip1.SetToolTip(this.cB_InvertHAxis, resources.GetString("cB_InvertHAxis.ToolTip"));
            this.cB_InvertHAxis.UseVisualStyleBackColor = true;
            // 
            // lB_InvertVAxis
            // 
            resources.ApplyResources(this.lB_InvertVAxis, "lB_InvertVAxis");
            this.lB_InvertVAxis.Name = "lB_InvertVAxis";
            this.toolTip1.SetToolTip(this.lB_InvertVAxis, resources.GetString("lB_InvertVAxis.ToolTip"));
            // 
            // lB_InvertHAxis
            // 
            resources.ApplyResources(this.lB_InvertHAxis, "lB_InvertHAxis");
            this.lB_InvertHAxis.Name = "lB_InvertHAxis";
            this.toolTip1.SetToolTip(this.lB_InvertHAxis, resources.GetString("lB_InvertHAxis.ToolTip"));
            // 
            // lB_GyroSteering
            // 
            resources.ApplyResources(this.lB_GyroSteering, "lB_GyroSteering");
            this.lB_GyroSteering.Name = "lB_GyroSteering";
            this.toolTip1.SetToolTip(this.lB_GyroSteering, resources.GetString("lB_GyroSteering.ToolTip"));
            // 
            // cB_GyroSteering
            // 
            resources.ApplyResources(this.cB_GyroSteering, "cB_GyroSteering");
            this.cB_GyroSteering.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_GyroSteering.FormattingEnabled = true;
            this.cB_GyroSteering.Items.AddRange(new object[] {
            resources.GetString("cB_GyroSteering.Items"),
            resources.GetString("cB_GyroSteering.Items1")});
            this.cB_GyroSteering.Name = "cB_GyroSteering";
            this.toolTip1.SetToolTip(this.cB_GyroSteering, resources.GetString("cB_GyroSteering.ToolTip"));
            // 
            // tb_ProfileAcceleroValue
            // 
            resources.ApplyResources(this.tb_ProfileAcceleroValue, "tb_ProfileAcceleroValue");
            this.tb_ProfileAcceleroValue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tb_ProfileAcceleroValue.LargeChange = 2;
            this.tb_ProfileAcceleroValue.Maximum = 30;
            this.tb_ProfileAcceleroValue.Minimum = 1;
            this.tb_ProfileAcceleroValue.Name = "tb_ProfileAcceleroValue";
            this.tb_ProfileAcceleroValue.TickFrequency = 2;
            this.toolTip1.SetToolTip(this.tb_ProfileAcceleroValue, resources.GetString("tb_ProfileAcceleroValue.ToolTip"));
            this.tb_ProfileAcceleroValue.Value = 10;
            this.tb_ProfileAcceleroValue.ValueChanged += new System.EventHandler(this.tb_ProfileAcceleroValue_Scroll);
            // 
            // lb_ProfileGyro
            // 
            resources.ApplyResources(this.lb_ProfileGyro, "lb_ProfileGyro");
            this.lb_ProfileGyro.Name = "lb_ProfileGyro";
            this.toolTip1.SetToolTip(this.lb_ProfileGyro, resources.GetString("lb_ProfileGyro.ToolTip"));
            // 
            // lb_ProfileAccelero
            // 
            resources.ApplyResources(this.lb_ProfileAccelero, "lb_ProfileAccelero");
            this.lb_ProfileAccelero.Name = "lb_ProfileAccelero";
            this.toolTip1.SetToolTip(this.lb_ProfileAccelero, resources.GetString("lb_ProfileAccelero.ToolTip"));
            // 
            // tb_ProfileGyroValue
            // 
            resources.ApplyResources(this.tb_ProfileGyroValue, "tb_ProfileGyroValue");
            this.tb_ProfileGyroValue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tb_ProfileGyroValue.LargeChange = 2;
            this.tb_ProfileGyroValue.Maximum = 30;
            this.tb_ProfileGyroValue.Minimum = 1;
            this.tb_ProfileGyroValue.Name = "tb_ProfileGyroValue";
            this.tb_ProfileGyroValue.TickFrequency = 2;
            this.toolTip1.SetToolTip(this.tb_ProfileGyroValue, resources.GetString("tb_ProfileGyroValue.ToolTip"));
            this.tb_ProfileGyroValue.Value = 10;
            this.tb_ProfileGyroValue.ValueChanged += new System.EventHandler(this.tb_ProfileGyroValue_Scroll);
            // 
            // gB_ProfileDetails
            // 
            resources.ApplyResources(this.gB_ProfileDetails, "gB_ProfileDetails");
            this.gB_ProfileDetails.Controls.Add(this.lb_ErrorCode);
            this.gB_ProfileDetails.Controls.Add(this.b_ApplyProfile);
            this.gB_ProfileDetails.Controls.Add(this.b_DeleteProfile);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfileName);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfileName);
            this.gB_ProfileDetails.Name = "gB_ProfileDetails";
            this.gB_ProfileDetails.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_ProfileDetails, resources.GetString("gB_ProfileDetails.ToolTip"));
            // 
            // lb_ErrorCode
            // 
            resources.ApplyResources(this.lb_ErrorCode, "lb_ErrorCode");
            this.lb_ErrorCode.ForeColor = System.Drawing.Color.Brown;
            this.lb_ErrorCode.Name = "lb_ErrorCode";
            this.toolTip1.SetToolTip(this.lb_ErrorCode, resources.GetString("lb_ErrorCode.ToolTip"));
            // 
            // b_ApplyProfile
            // 
            resources.ApplyResources(this.b_ApplyProfile, "b_ApplyProfile");
            this.b_ApplyProfile.Name = "b_ApplyProfile";
            this.toolTip1.SetToolTip(this.b_ApplyProfile, resources.GetString("b_ApplyProfile.ToolTip"));
            this.b_ApplyProfile.UseVisualStyleBackColor = true;
            this.b_ApplyProfile.Click += new System.EventHandler(this.b_ApplyProfile_Click);
            // 
            // b_DeleteProfile
            // 
            resources.ApplyResources(this.b_DeleteProfile, "b_DeleteProfile");
            this.b_DeleteProfile.Name = "b_DeleteProfile";
            this.toolTip1.SetToolTip(this.b_DeleteProfile, resources.GetString("b_DeleteProfile.ToolTip"));
            this.b_DeleteProfile.UseVisualStyleBackColor = true;
            this.b_DeleteProfile.Click += new System.EventHandler(this.b_DeleteProfile_Click);
            // 
            // tB_ProfilePath
            // 
            resources.ApplyResources(this.tB_ProfilePath, "tB_ProfilePath");
            this.tB_ProfilePath.Name = "tB_ProfilePath";
            this.tB_ProfilePath.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tB_ProfilePath, resources.GetString("tB_ProfilePath.ToolTip"));
            // 
            // lb_ProfilePath
            // 
            resources.ApplyResources(this.lb_ProfilePath, "lb_ProfilePath");
            this.lb_ProfilePath.Name = "lb_ProfilePath";
            this.toolTip1.SetToolTip(this.lb_ProfilePath, resources.GetString("lb_ProfilePath.ToolTip"));
            // 
            // tB_ProfileName
            // 
            resources.ApplyResources(this.tB_ProfileName, "tB_ProfileName");
            this.tB_ProfileName.Name = "tB_ProfileName";
            this.tB_ProfileName.ReadOnly = true;
            this.toolTip1.SetToolTip(this.tB_ProfileName, resources.GetString("tB_ProfileName.ToolTip"));
            // 
            // lb_ProfileName
            // 
            resources.ApplyResources(this.lb_ProfileName, "lb_ProfileName");
            this.lb_ProfileName.Name = "lb_ProfileName";
            this.toolTip1.SetToolTip(this.lb_ProfileName, resources.GetString("lb_ProfileName.ToolTip"));
            // 
            // gB_Profiles
            // 
            resources.ApplyResources(this.gB_Profiles, "gB_Profiles");
            this.gB_Profiles.Controls.Add(this.b_CreateProfile);
            this.gB_Profiles.Controls.Add(this.lB_Profiles);
            this.gB_Profiles.Name = "gB_Profiles";
            this.gB_Profiles.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_Profiles, resources.GetString("gB_Profiles.ToolTip"));
            // 
            // b_CreateProfile
            // 
            resources.ApplyResources(this.b_CreateProfile, "b_CreateProfile");
            this.b_CreateProfile.Name = "b_CreateProfile";
            this.toolTip1.SetToolTip(this.b_CreateProfile, resources.GetString("b_CreateProfile.ToolTip"));
            this.b_CreateProfile.UseVisualStyleBackColor = true;
            this.b_CreateProfile.Click += new System.EventHandler(this.b_CreateProfile_Click);
            // 
            // lB_Profiles
            // 
            resources.ApplyResources(this.lB_Profiles, "lB_Profiles");
            this.lB_Profiles.FormattingEnabled = true;
            this.lB_Profiles.Name = "lB_Profiles";
            this.toolTip1.SetToolTip(this.lB_Profiles, resources.GetString("lB_Profiles.ToolTip"));
            this.lB_Profiles.SelectedIndexChanged += new System.EventHandler(this.lB_Profiles_SelectedIndexChanged);
            // 
            // gB_ProfileGyro
            // 
            resources.ApplyResources(this.gB_ProfileGyro, "gB_ProfileGyro");
            this.gB_ProfileGyro.Controls.Add(this.tB_UMCIntensity);
            this.gB_ProfileGyro.Controls.Add(this.cB_UMCInputButton);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCInputButton);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCIntensity);
            this.gB_ProfileGyro.Controls.Add(this.tB_UMCSensivity);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCSensivity);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCInputStyle);
            this.gB_ProfileGyro.Controls.Add(this.cB_UMCInputStyle);
            this.gB_ProfileGyro.Name = "gB_ProfileGyro";
            this.gB_ProfileGyro.TabStop = false;
            this.toolTip1.SetToolTip(this.gB_ProfileGyro, resources.GetString("gB_ProfileGyro.ToolTip"));
            // 
            // tB_UMCIntensity
            // 
            resources.ApplyResources(this.tB_UMCIntensity, "tB_UMCIntensity");
            this.tB_UMCIntensity.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_UMCIntensity.LargeChange = 2;
            this.tB_UMCIntensity.Maximum = 20;
            this.tB_UMCIntensity.Minimum = 1;
            this.tB_UMCIntensity.Name = "tB_UMCIntensity";
            this.toolTip1.SetToolTip(this.tB_UMCIntensity, resources.GetString("tB_UMCIntensity.ToolTip"));
            this.tB_UMCIntensity.Value = 1;
            this.tB_UMCIntensity.Scroll += new System.EventHandler(this.tB_UMCIntensity_Scroll);
            // 
            // cB_UMCInputButton
            // 
            resources.ApplyResources(this.cB_UMCInputButton, "cB_UMCInputButton");
            this.cB_UMCInputButton.FormattingEnabled = true;
            this.cB_UMCInputButton.Name = "cB_UMCInputButton";
            this.cB_UMCInputButton.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.cB_UMCInputButton.Sorted = true;
            this.toolTip1.SetToolTip(this.cB_UMCInputButton, resources.GetString("cB_UMCInputButton.ToolTip"));
            // 
            // lb_UMCInputButton
            // 
            resources.ApplyResources(this.lb_UMCInputButton, "lb_UMCInputButton");
            this.lb_UMCInputButton.Name = "lb_UMCInputButton";
            this.toolTip1.SetToolTip(this.lb_UMCInputButton, resources.GetString("lb_UMCInputButton.ToolTip"));
            // 
            // lb_UMCIntensity
            // 
            resources.ApplyResources(this.lb_UMCIntensity, "lb_UMCIntensity");
            this.lb_UMCIntensity.Name = "lb_UMCIntensity";
            this.toolTip1.SetToolTip(this.lb_UMCIntensity, resources.GetString("lb_UMCIntensity.ToolTip"));
            // 
            // tB_UMCSensivity
            // 
            resources.ApplyResources(this.tB_UMCSensivity, "tB_UMCSensivity");
            this.tB_UMCSensivity.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_UMCSensivity.LargeChange = 2;
            this.tB_UMCSensivity.Maximum = 20;
            this.tB_UMCSensivity.Minimum = 1;
            this.tB_UMCSensivity.Name = "tB_UMCSensivity";
            this.toolTip1.SetToolTip(this.tB_UMCSensivity, resources.GetString("tB_UMCSensivity.ToolTip"));
            this.tB_UMCSensivity.Value = 1;
            this.tB_UMCSensivity.Scroll += new System.EventHandler(this.tB_UMCSensivity_Scroll);
            // 
            // lb_UMCSensivity
            // 
            resources.ApplyResources(this.lb_UMCSensivity, "lb_UMCSensivity");
            this.lb_UMCSensivity.Name = "lb_UMCSensivity";
            this.toolTip1.SetToolTip(this.lb_UMCSensivity, resources.GetString("lb_UMCSensivity.ToolTip"));
            // 
            // lb_UMCInputStyle
            // 
            resources.ApplyResources(this.lb_UMCInputStyle, "lb_UMCInputStyle");
            this.lb_UMCInputStyle.Name = "lb_UMCInputStyle";
            this.toolTip1.SetToolTip(this.lb_UMCInputStyle, resources.GetString("lb_UMCInputStyle.ToolTip"));
            // 
            // cB_UMCInputStyle
            // 
            resources.ApplyResources(this.cB_UMCInputStyle, "cB_UMCInputStyle");
            this.cB_UMCInputStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_UMCInputStyle.FormattingEnabled = true;
            this.cB_UMCInputStyle.Items.AddRange(new object[] {
            resources.GetString("cB_UMCInputStyle.Items"),
            resources.GetString("cB_UMCInputStyle.Items1"),
            resources.GetString("cB_UMCInputStyle.Items2"),
            resources.GetString("cB_UMCInputStyle.Items3")});
            this.cB_UMCInputStyle.Name = "cB_UMCInputStyle";
            this.toolTip1.SetToolTip(this.cB_UMCInputStyle, resources.GetString("cB_UMCInputStyle.ToolTip"));
            this.cB_UMCInputStyle.SelectedIndexChanged += new System.EventHandler(this.cB_UMCInputStyle_SelectedIndexChanged);
            // 
            // tabSettings
            // 
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Controls.Add(this.gb_SettingsUDP);
            this.tabSettings.Controls.Add(this.gb_SettingsService);
            this.tabSettings.Controls.Add(this.gb_SettingsInterface);
            this.tabSettings.Name = "tabSettings";
            this.toolTip1.SetToolTip(this.tabSettings, resources.GetString("tabSettings.ToolTip"));
            this.tabSettings.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsUDP
            // 
            resources.ApplyResources(this.gb_SettingsUDP, "gb_SettingsUDP");
            this.gb_SettingsUDP.Controls.Add(this.b_UDPApply);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPPort);
            this.gb_SettingsUDP.Controls.Add(this.lb_UDPport);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPIP);
            this.gb_SettingsUDP.Controls.Add(this.cB_UDPEnable);
            this.gb_SettingsUDP.Name = "gb_SettingsUDP";
            this.gb_SettingsUDP.TabStop = false;
            this.toolTip1.SetToolTip(this.gb_SettingsUDP, resources.GetString("gb_SettingsUDP.ToolTip"));
            // 
            // b_UDPApply
            // 
            resources.ApplyResources(this.b_UDPApply, "b_UDPApply");
            this.b_UDPApply.Name = "b_UDPApply";
            this.toolTip1.SetToolTip(this.b_UDPApply, resources.GetString("b_UDPApply.ToolTip"));
            this.b_UDPApply.UseVisualStyleBackColor = true;
            this.b_UDPApply.Click += new System.EventHandler(this.b_UDPApply_Click);
            // 
            // tB_UDPPort
            // 
            resources.ApplyResources(this.tB_UDPPort, "tB_UDPPort");
            this.tB_UDPPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.tB_UDPPort.Name = "tB_UDPPort";
            this.toolTip1.SetToolTip(this.tB_UDPPort, resources.GetString("tB_UDPPort.ToolTip"));
            this.tB_UDPPort.Value = new decimal(new int[] {
            26760,
            0,
            0,
            0});
            // 
            // lb_UDPport
            // 
            resources.ApplyResources(this.lb_UDPport, "lb_UDPport");
            this.lb_UDPport.Name = "lb_UDPport";
            this.toolTip1.SetToolTip(this.lb_UDPport, resources.GetString("lb_UDPport.ToolTip"));
            // 
            // tB_UDPIP
            // 
            resources.ApplyResources(this.tB_UDPIP, "tB_UDPIP");
            this.tB_UDPIP.Name = "tB_UDPIP";
            this.toolTip1.SetToolTip(this.tB_UDPIP, resources.GetString("tB_UDPIP.ToolTip"));
            // 
            // cB_UDPEnable
            // 
            resources.ApplyResources(this.cB_UDPEnable, "cB_UDPEnable");
            this.cB_UDPEnable.Name = "cB_UDPEnable";
            this.toolTip1.SetToolTip(this.cB_UDPEnable, resources.GetString("cB_UDPEnable.ToolTip"));
            this.cB_UDPEnable.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsService
            // 
            resources.ApplyResources(this.gb_SettingsService, "gb_SettingsService");
            this.gb_SettingsService.Controls.Add(this.lB_ServiceStartup);
            this.gb_SettingsService.Controls.Add(this.lB_ServiceStatus);
            this.gb_SettingsService.Controls.Add(this.cB_ServiceStartup);
            this.gb_SettingsService.Controls.Add(this.b_ServiceStop);
            this.gb_SettingsService.Controls.Add(this.b_ServiceStart);
            this.gb_SettingsService.Controls.Add(this.b_ServiceDelete);
            this.gb_SettingsService.Controls.Add(this.b_ServiceInstall);
            this.gb_SettingsService.Name = "gb_SettingsService";
            this.gb_SettingsService.TabStop = false;
            this.toolTip1.SetToolTip(this.gb_SettingsService, resources.GetString("gb_SettingsService.ToolTip"));
            // 
            // lB_ServiceStartup
            // 
            resources.ApplyResources(this.lB_ServiceStartup, "lB_ServiceStartup");
            this.lB_ServiceStartup.Name = "lB_ServiceStartup";
            this.toolTip1.SetToolTip(this.lB_ServiceStartup, resources.GetString("lB_ServiceStartup.ToolTip"));
            // 
            // lB_ServiceStatus
            // 
            resources.ApplyResources(this.lB_ServiceStatus, "lB_ServiceStatus");
            this.lB_ServiceStatus.Name = "lB_ServiceStatus";
            this.toolTip1.SetToolTip(this.lB_ServiceStatus, resources.GetString("lB_ServiceStatus.ToolTip"));
            // 
            // cB_ServiceStartup
            // 
            resources.ApplyResources(this.cB_ServiceStartup, "cB_ServiceStartup");
            this.cB_ServiceStartup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_ServiceStartup.FormattingEnabled = true;
            this.cB_ServiceStartup.Items.AddRange(new object[] {
            resources.GetString("cB_ServiceStartup.Items"),
            resources.GetString("cB_ServiceStartup.Items1"),
            resources.GetString("cB_ServiceStartup.Items2")});
            this.cB_ServiceStartup.Name = "cB_ServiceStartup";
            this.toolTip1.SetToolTip(this.cB_ServiceStartup, resources.GetString("cB_ServiceStartup.ToolTip"));
            this.cB_ServiceStartup.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // b_ServiceStop
            // 
            resources.ApplyResources(this.b_ServiceStop, "b_ServiceStop");
            this.b_ServiceStop.Name = "b_ServiceStop";
            this.toolTip1.SetToolTip(this.b_ServiceStop, resources.GetString("b_ServiceStop.ToolTip"));
            this.b_ServiceStop.UseVisualStyleBackColor = true;
            this.b_ServiceStop.Click += new System.EventHandler(this.b_ServiceStop_Click);
            // 
            // b_ServiceStart
            // 
            resources.ApplyResources(this.b_ServiceStart, "b_ServiceStart");
            this.b_ServiceStart.Name = "b_ServiceStart";
            this.toolTip1.SetToolTip(this.b_ServiceStart, resources.GetString("b_ServiceStart.ToolTip"));
            this.b_ServiceStart.UseVisualStyleBackColor = true;
            this.b_ServiceStart.Click += new System.EventHandler(this.b_ServiceStart_Click);
            // 
            // b_ServiceDelete
            // 
            resources.ApplyResources(this.b_ServiceDelete, "b_ServiceDelete");
            this.b_ServiceDelete.Name = "b_ServiceDelete";
            this.toolTip1.SetToolTip(this.b_ServiceDelete, resources.GetString("b_ServiceDelete.ToolTip"));
            this.b_ServiceDelete.UseVisualStyleBackColor = true;
            this.b_ServiceDelete.Click += new System.EventHandler(this.b_ServiceDelete_Click);
            // 
            // b_ServiceInstall
            // 
            resources.ApplyResources(this.b_ServiceInstall, "b_ServiceInstall");
            this.b_ServiceInstall.Name = "b_ServiceInstall";
            this.toolTip1.SetToolTip(this.b_ServiceInstall, resources.GetString("b_ServiceInstall.ToolTip"));
            this.b_ServiceInstall.UseVisualStyleBackColor = true;
            this.b_ServiceInstall.Click += new System.EventHandler(this.b_ServiceInstall_Click);
            // 
            // gb_SettingsInterface
            // 
            resources.ApplyResources(this.gb_SettingsInterface, "gb_SettingsInterface");
            this.gb_SettingsInterface.Controls.Add(this.cB_RunAtStartup);
            this.gb_SettingsInterface.Controls.Add(this.cB_CloseMinimizes);
            this.gb_SettingsInterface.Controls.Add(this.cB_StartMinimized);
            this.gb_SettingsInterface.Name = "gb_SettingsInterface";
            this.gb_SettingsInterface.TabStop = false;
            this.toolTip1.SetToolTip(this.gb_SettingsInterface, resources.GetString("gb_SettingsInterface.ToolTip"));
            // 
            // cB_RunAtStartup
            // 
            resources.ApplyResources(this.cB_RunAtStartup, "cB_RunAtStartup");
            this.cB_RunAtStartup.Name = "cB_RunAtStartup";
            this.toolTip1.SetToolTip(this.cB_RunAtStartup, resources.GetString("cB_RunAtStartup.ToolTip"));
            this.cB_RunAtStartup.UseVisualStyleBackColor = true;
            this.cB_RunAtStartup.CheckedChanged += new System.EventHandler(this.cB_RunAtStartup_CheckedChanged);
            // 
            // cB_CloseMinimizes
            // 
            resources.ApplyResources(this.cB_CloseMinimizes, "cB_CloseMinimizes");
            this.cB_CloseMinimizes.Name = "cB_CloseMinimizes";
            this.toolTip1.SetToolTip(this.cB_CloseMinimizes, resources.GetString("cB_CloseMinimizes.ToolTip"));
            this.cB_CloseMinimizes.UseVisualStyleBackColor = true;
            this.cB_CloseMinimizes.CheckedChanged += new System.EventHandler(this.cB_CloseMinimizes_CheckedChanged);
            // 
            // cB_StartMinimized
            // 
            resources.ApplyResources(this.cB_StartMinimized, "cB_StartMinimized");
            this.cB_StartMinimized.Name = "cB_StartMinimized";
            this.toolTip1.SetToolTip(this.cB_StartMinimized, resources.GetString("cB_StartMinimized.ToolTip"));
            this.cB_StartMinimized.UseVisualStyleBackColor = true;
            this.cB_StartMinimized.CheckedChanged += new System.EventHandler(this.cB_StartMinimized_CheckedChanged);
            // 
            // tabAbout
            // 
            resources.ApplyResources(this.tabAbout, "tabAbout");
            this.tabAbout.Controls.Add(this.lL_AboutDonate);
            this.tabAbout.Controls.Add(this.lL_AboutWiki);
            this.tabAbout.Controls.Add(this.lL_AboutSource);
            this.tabAbout.Controls.Add(this.lb_AboutDescription);
            this.tabAbout.Controls.Add(this.lb_AboutAuthor);
            this.tabAbout.Controls.Add(this.lb_AboutVersion);
            this.tabAbout.Controls.Add(this.lb_AboutTitle);
            this.tabAbout.Controls.Add(this.pB_About);
            this.tabAbout.Name = "tabAbout";
            this.toolTip1.SetToolTip(this.tabAbout, resources.GetString("tabAbout.ToolTip"));
            this.tabAbout.UseVisualStyleBackColor = true;
            // 
            // lL_AboutDonate
            // 
            resources.ApplyResources(this.lL_AboutDonate, "lL_AboutDonate");
            this.lL_AboutDonate.Name = "lL_AboutDonate";
            this.lL_AboutDonate.TabStop = true;
            this.toolTip1.SetToolTip(this.lL_AboutDonate, resources.GetString("lL_AboutDonate.ToolTip"));
            this.lL_AboutDonate.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.IL_AboutDonate_LinkClicked);
            // 
            // lL_AboutWiki
            // 
            resources.ApplyResources(this.lL_AboutWiki, "lL_AboutWiki");
            this.lL_AboutWiki.Name = "lL_AboutWiki";
            this.lL_AboutWiki.TabStop = true;
            this.toolTip1.SetToolTip(this.lL_AboutWiki, resources.GetString("lL_AboutWiki.ToolTip"));
            this.lL_AboutWiki.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.IL_AboutWiki_LinkClicked);
            // 
            // lL_AboutSource
            // 
            resources.ApplyResources(this.lL_AboutSource, "lL_AboutSource");
            this.lL_AboutSource.Name = "lL_AboutSource";
            this.lL_AboutSource.TabStop = true;
            this.toolTip1.SetToolTip(this.lL_AboutSource, resources.GetString("lL_AboutSource.ToolTip"));
            this.lL_AboutSource.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.IL_AboutSource_LinkClicked);
            // 
            // lb_AboutDescription
            // 
            resources.ApplyResources(this.lb_AboutDescription, "lb_AboutDescription");
            this.lb_AboutDescription.Name = "lb_AboutDescription";
            this.toolTip1.SetToolTip(this.lb_AboutDescription, resources.GetString("lb_AboutDescription.ToolTip"));
            // 
            // lb_AboutAuthor
            // 
            resources.ApplyResources(this.lb_AboutAuthor, "lb_AboutAuthor");
            this.lb_AboutAuthor.Name = "lb_AboutAuthor";
            this.toolTip1.SetToolTip(this.lb_AboutAuthor, resources.GetString("lb_AboutAuthor.ToolTip"));
            // 
            // lb_AboutVersion
            // 
            resources.ApplyResources(this.lb_AboutVersion, "lb_AboutVersion");
            this.lb_AboutVersion.Name = "lb_AboutVersion";
            this.toolTip1.SetToolTip(this.lb_AboutVersion, resources.GetString("lb_AboutVersion.ToolTip"));
            // 
            // lb_AboutTitle
            // 
            resources.ApplyResources(this.lb_AboutTitle, "lb_AboutTitle");
            this.lb_AboutTitle.Name = "lb_AboutTitle";
            this.toolTip1.SetToolTip(this.lb_AboutTitle, resources.GetString("lb_AboutTitle.ToolTip"));
            // 
            // pB_AboutPicture
            // 
            resources.ApplyResources(this.pB_About, "pB_AboutPicture");
            this.pB_About.BackgroundImage = global::ControllerHelper.Properties.Resources.logo_playstation1;
            this.pB_About.Name = "pB_AboutPicture";
            this.pB_About.TabStop = false;
            this.toolTip1.SetToolTip(this.pB_About, resources.GetString("pB_AboutPicture.ToolTip"));
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            resources.ApplyResources(this.openFileDialog1, "openFileDialog1");
            // 
            // toolTip1
            // 
            this.toolTip1.AutoPopDelay = 5000;
            this.toolTip1.InitialDelay = 100;
            this.toolTip1.ReshowDelay = 100;
            // 
            // ControllerHelper
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.Name = "ControllerHelper";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.toolTip1.SetToolTip(this, resources.GetString("$this.ToolTip"));
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ControllerHelper_Close);
            this.Load += new System.EventHandler(this.ControllerHelper_Load);
            this.Resize += new System.EventHandler(this.ControllerHelper_Resize);
            this.contextMenuStrip1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabDevices.ResumeLayout(false);
            this.gB_DeviceDetails.ResumeLayout(false);
            this.gB_DeviceDetails.PerformLayout();
            this.gB_HIDDetails.ResumeLayout(false);
            this.gB_HIDDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pB_HidMode)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_VibrationStr)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_PullRate)).EndInit();
            this.gB_XinputDetails.ResumeLayout(false);
            this.gB_XinputDetails.PerformLayout();
            this.gB_XinputDevices.ResumeLayout(false);
            this.tabProfiles.ResumeLayout(false);
            this.gB_ProfileOptions.ResumeLayout(false);
            this.gB_ProfileOptions.PerformLayout();
            this.gB_6axis.ResumeLayout(false);
            this.gB_6axis.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileAcceleroValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileGyroValue)).EndInit();
            this.gB_ProfileDetails.ResumeLayout(false);
            this.gB_ProfileDetails.PerformLayout();
            this.gB_Profiles.ResumeLayout(false);
            this.gB_ProfileGyro.ResumeLayout(false);
            this.gB_ProfileGyro.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UMCIntensity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UMCSensivity)).EndInit();
            this.tabSettings.ResumeLayout(false);
            this.gb_SettingsUDP.ResumeLayout(false);
            this.gb_SettingsUDP.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UDPPort)).EndInit();
            this.gb_SettingsService.ResumeLayout(false);
            this.gb_SettingsService.PerformLayout();
            this.gb_SettingsInterface.ResumeLayout(false);
            this.gb_SettingsInterface.PerformLayout();
            this.tabAbout.ResumeLayout(false);
            this.tabAbout.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pB_About)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem quitToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDevices;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.GroupBox gB_XinputDevices;
        private System.Windows.Forms.GroupBox gB_XinputDetails;
        private System.Windows.Forms.ListBox lB_Devices;
        private System.Windows.Forms.Label lb_InstanceID;
        private System.Windows.Forms.TextBox tB_InstanceID;
        private System.Windows.Forms.ComboBox cB_HidMode;
        private System.Windows.Forms.Label lb_HidMode;
        private System.Windows.Forms.Label lb_HidCloak;
        private System.Windows.Forms.GroupBox gB_HIDDetails;
        private System.Windows.Forms.Label lb_gyro;
        private System.Windows.Forms.CheckBox cB_gyro;
        private System.Windows.Forms.CheckBox cB_accelero;
        private System.Windows.Forms.Label lb_accelero;
        private System.Windows.Forms.Label lb_PullRate;
        private System.Windows.Forms.TrackBar tB_PullRate;
        private System.Windows.Forms.TabPage tabProfiles;
        private System.Windows.Forms.GroupBox gb_SettingsInterface;
        private System.Windows.Forms.CheckBox cB_RunAtStartup;
        private System.Windows.Forms.CheckBox cB_CloseMinimizes;
        private System.Windows.Forms.CheckBox cB_StartMinimized;
        private System.Windows.Forms.GroupBox gb_SettingsUDP;
        private System.Windows.Forms.CheckBox cB_UDPEnable;
        private System.Windows.Forms.TextBox tB_UDPIP;
        private System.Windows.Forms.Label lb_UDPport;
        private System.Windows.Forms.NumericUpDown tB_UDPPort;
        private System.Windows.Forms.Button b_UDPApply;
        private System.Windows.Forms.CheckBox cB_uncloak;
        private System.Windows.Forms.GroupBox gB_DeviceDetails;
        private System.Windows.Forms.GroupBox gB_Profiles;
        private System.Windows.Forms.ListBox lB_Profiles;
        private System.Windows.Forms.GroupBox gB_ProfileDetails;
        private System.Windows.Forms.TextBox tB_ProfilePath;
        private System.Windows.Forms.Label lb_ProfilePath;
        private System.Windows.Forms.TextBox tB_ProfileName;
        private System.Windows.Forms.Label lb_ProfileName;
        private System.Windows.Forms.Label lb_Whitelist;
        private System.Windows.Forms.CheckBox cB_Whitelist;
        private System.Windows.Forms.Label lb_Wrapper;
        private System.Windows.Forms.CheckBox cB_Wrapper;
        private System.Windows.Forms.GroupBox gB_ProfileOptions;
        private System.Windows.Forms.GroupBox gb_SettingsService;
        private System.Windows.Forms.CheckBox cB_touchpad;
        private System.Windows.Forms.Label lb_touchpad;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Button b_CreateProfile;
        private System.Windows.Forms.Button b_DeleteProfile;
        private System.Windows.Forms.Button b_ServiceDelete;
        private System.Windows.Forms.Button b_ServiceInstall;
        private System.Windows.Forms.Button b_ServiceStop;
        private System.Windows.Forms.Button b_ServiceStart;
        private System.Windows.Forms.TrackBar tb_ProfileGyroValue;
        private System.Windows.Forms.Label lb_ProfileGyro;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TrackBar tb_ProfileAcceleroValue;
        private System.Windows.Forms.Label lb_ProfileAccelero;
        private System.Windows.Forms.Button b_ApplyProfile;
        private System.Windows.Forms.TextBox tB_ProductID;
        private System.Windows.Forms.Label lb_ProductID;
        private System.Windows.Forms.TrackBar tB_VibrationStr;
        private System.Windows.Forms.Label lb_VibrationStr;
        private System.Windows.Forms.Label lB_ServiceStatus;
        private System.Windows.Forms.ComboBox cB_ServiceStartup;
        private System.Windows.Forms.Label lB_ServiceStartup;
        private System.Windows.Forms.GroupBox gB_6axis;
        private System.Windows.Forms.Label lB_GyroSteering;
        private System.Windows.Forms.ComboBox cB_GyroSteering;
        private System.Windows.Forms.Label lB_InvertHAxis;
        private System.Windows.Forms.Label lB_InvertVAxis;
        private System.Windows.Forms.CheckBox cB_HIDcloak;
        private System.Windows.Forms.CheckBox cB_InvertVAxis;
        private System.Windows.Forms.CheckBox cB_InvertHAxis;
        private System.Windows.Forms.Label lb_UniversalMC;
        private System.Windows.Forms.ComboBox cB_UMCInputStyle;
        private System.Windows.Forms.GroupBox gB_ProfileGyro;
        private System.Windows.Forms.CheckBox cB_UniversalMC;
        private System.Windows.Forms.Label lb_UMCInputStyle;
        private System.Windows.Forms.TrackBar tB_UMCSensivity;
        private System.Windows.Forms.Label lb_UMCSensivity;
        private System.Windows.Forms.Label lb_UMCIntensity;
        private System.Windows.Forms.Label lb_UMCInputButton;
        private System.Windows.Forms.ListBox cB_UMCInputButton;
        private System.Windows.Forms.TrackBar tB_UMCIntensity;
        private System.Windows.Forms.TabPage tabAbout;
        private System.Windows.Forms.PictureBox pB_About;
        private System.Windows.Forms.Label lb_AboutTitle;
        private System.Windows.Forms.Label lb_AboutVersion;
        private System.Windows.Forms.Label lb_AboutAuthor;
        private System.Windows.Forms.Label lb_AboutDescription;
        private System.Windows.Forms.LinkLabel lL_AboutSource;
        private System.Windows.Forms.LinkLabel lL_AboutDonate;
        private System.Windows.Forms.LinkLabel lL_AboutWiki;
        private System.Windows.Forms.Label lb_ErrorCode;
        private System.Windows.Forms.PictureBox pB_HidMode;
    }
}

