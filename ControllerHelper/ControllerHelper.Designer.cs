
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
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabDevices.SuspendLayout();
            this.gB_DeviceDetails.SuspendLayout();
            this.gB_HIDDetails.SuspendLayout();
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
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            resources.ApplyResources(this.notifyIcon1, "notifyIcon1");
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.notifyIcon1_DoubleClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quitToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            resources.ApplyResources(this.contextMenuStrip1, "contextMenuStrip1");
            // 
            // quitToolStripMenuItem
            // 
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            resources.ApplyResources(this.quitToolStripMenuItem, "quitToolStripMenuItem");
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.quitToolStripMenuItem_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabDevices);
            this.tabControl1.Controls.Add(this.tabProfiles);
            this.tabControl1.Controls.Add(this.tabSettings);
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            // 
            // tabDevices
            // 
            this.tabDevices.Controls.Add(this.gB_DeviceDetails);
            this.tabDevices.Controls.Add(this.gB_HIDDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDevices);
            resources.ApplyResources(this.tabDevices, "tabDevices");
            this.tabDevices.Name = "tabDevices";
            this.tabDevices.UseVisualStyleBackColor = true;
            // 
            // gB_DeviceDetails
            // 
            this.gB_DeviceDetails.Controls.Add(this.cB_touchpad);
            this.gB_DeviceDetails.Controls.Add(this.lb_touchpad);
            this.gB_DeviceDetails.Controls.Add(this.lb_gyro);
            this.gB_DeviceDetails.Controls.Add(this.cB_accelero);
            this.gB_DeviceDetails.Controls.Add(this.cB_gyro);
            this.gB_DeviceDetails.Controls.Add(this.lb_accelero);
            resources.ApplyResources(this.gB_DeviceDetails, "gB_DeviceDetails");
            this.gB_DeviceDetails.Name = "gB_DeviceDetails";
            this.gB_DeviceDetails.TabStop = false;
            // 
            // cB_touchpad
            // 
            resources.ApplyResources(this.cB_touchpad, "cB_touchpad");
            this.cB_touchpad.Name = "cB_touchpad";
            this.cB_touchpad.UseVisualStyleBackColor = true;
            this.cB_touchpad.CheckedChanged += new System.EventHandler(this.cB_touchpad_CheckedChanged);
            // 
            // lb_touchpad
            // 
            resources.ApplyResources(this.lb_touchpad, "lb_touchpad");
            this.lb_touchpad.Name = "lb_touchpad";
            // 
            // lb_gyro
            // 
            resources.ApplyResources(this.lb_gyro, "lb_gyro");
            this.lb_gyro.Name = "lb_gyro";
            // 
            // cB_accelero
            // 
            resources.ApplyResources(this.cB_accelero, "cB_accelero");
            this.cB_accelero.Name = "cB_accelero";
            this.cB_accelero.UseVisualStyleBackColor = true;
            // 
            // cB_gyro
            // 
            resources.ApplyResources(this.cB_gyro, "cB_gyro");
            this.cB_gyro.Name = "cB_gyro";
            this.cB_gyro.UseVisualStyleBackColor = true;
            // 
            // lb_accelero
            // 
            resources.ApplyResources(this.lb_accelero, "lb_accelero");
            this.lb_accelero.Name = "lb_accelero";
            // 
            // gB_HIDDetails
            // 
            this.gB_HIDDetails.Controls.Add(this.tB_VibrationStr);
            this.gB_HIDDetails.Controls.Add(this.lb_VibrationStr);
            this.gB_HIDDetails.Controls.Add(this.tB_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_HidMode);
            this.gB_HIDDetails.Controls.Add(this.cB_HidMode);
            resources.ApplyResources(this.gB_HIDDetails, "gB_HIDDetails");
            this.gB_HIDDetails.Name = "gB_HIDDetails";
            this.gB_HIDDetails.TabStop = false;
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
            this.tB_VibrationStr.Value = 50;
            this.tB_VibrationStr.ValueChanged += new System.EventHandler(this.tB_VibrationStr_Scroll);
            // 
            // lb_VibrationStr
            // 
            resources.ApplyResources(this.lb_VibrationStr, "lb_VibrationStr");
            this.lb_VibrationStr.Name = "lb_VibrationStr";
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
            this.tB_PullRate.Value = 10;
            this.tB_PullRate.ValueChanged += new System.EventHandler(this.tB_PullRate_Scroll);
            // 
            // lb_PullRate
            // 
            resources.ApplyResources(this.lb_PullRate, "lb_PullRate");
            this.lb_PullRate.Name = "lb_PullRate";
            // 
            // lb_HidMode
            // 
            resources.ApplyResources(this.lb_HidMode, "lb_HidMode");
            this.lb_HidMode.Name = "lb_HidMode";
            // 
            // cB_HidMode
            // 
            this.cB_HidMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HidMode.FormattingEnabled = true;
            resources.ApplyResources(this.cB_HidMode, "cB_HidMode");
            this.cB_HidMode.Name = "cB_HidMode";
            this.cB_HidMode.SelectedIndexChanged += new System.EventHandler(this.cB_HidMode_SelectedIndexChanged);
            // 
            // gB_XinputDetails
            // 
            this.gB_XinputDetails.Controls.Add(this.cB_HIDcloak);
            this.gB_XinputDetails.Controls.Add(this.tB_ProductID);
            this.gB_XinputDetails.Controls.Add(this.lb_ProductID);
            this.gB_XinputDetails.Controls.Add(this.cB_uncloak);
            this.gB_XinputDetails.Controls.Add(this.lb_HidCloak);
            this.gB_XinputDetails.Controls.Add(this.tB_InstanceID);
            this.gB_XinputDetails.Controls.Add(this.lb_InstanceID);
            resources.ApplyResources(this.gB_XinputDetails, "gB_XinputDetails");
            this.gB_XinputDetails.Name = "gB_XinputDetails";
            this.gB_XinputDetails.TabStop = false;
            // 
            // cB_HIDcloak
            // 
            resources.ApplyResources(this.cB_HIDcloak, "cB_HIDcloak");
            this.cB_HIDcloak.Name = "cB_HIDcloak";
            this.cB_HIDcloak.UseVisualStyleBackColor = true;
            this.cB_HIDcloak.CheckedChanged += new System.EventHandler(this.cB_HIDcloak_CheckedChanged);
            // 
            // tB_ProductID
            // 
            resources.ApplyResources(this.tB_ProductID, "tB_ProductID");
            this.tB_ProductID.Name = "tB_ProductID";
            this.tB_ProductID.ReadOnly = true;
            // 
            // lb_ProductID
            // 
            resources.ApplyResources(this.lb_ProductID, "lb_ProductID");
            this.lb_ProductID.Name = "lb_ProductID";
            // 
            // cB_uncloak
            // 
            resources.ApplyResources(this.cB_uncloak, "cB_uncloak");
            this.cB_uncloak.Name = "cB_uncloak";
            this.cB_uncloak.UseVisualStyleBackColor = true;
            this.cB_uncloak.CheckedChanged += new System.EventHandler(this.cB_uncloak_CheckedChanged);
            // 
            // lb_HidCloak
            // 
            resources.ApplyResources(this.lb_HidCloak, "lb_HidCloak");
            this.lb_HidCloak.Name = "lb_HidCloak";
            // 
            // tB_InstanceID
            // 
            resources.ApplyResources(this.tB_InstanceID, "tB_InstanceID");
            this.tB_InstanceID.Name = "tB_InstanceID";
            this.tB_InstanceID.ReadOnly = true;
            // 
            // lb_InstanceID
            // 
            resources.ApplyResources(this.lb_InstanceID, "lb_InstanceID");
            this.lb_InstanceID.Name = "lb_InstanceID";
            // 
            // gB_XinputDevices
            // 
            this.gB_XinputDevices.Controls.Add(this.lB_Devices);
            resources.ApplyResources(this.gB_XinputDevices, "gB_XinputDevices");
            this.gB_XinputDevices.Name = "gB_XinputDevices";
            this.gB_XinputDevices.TabStop = false;
            // 
            // lB_Devices
            // 
            resources.ApplyResources(this.lB_Devices, "lB_Devices");
            this.lB_Devices.FormattingEnabled = true;
            this.lB_Devices.Name = "lB_Devices";
            this.lB_Devices.SelectedIndexChanged += new System.EventHandler(this.lB_Devices_SelectedIndexChanged);
            // 
            // tabProfiles
            // 
            this.tabProfiles.Controls.Add(this.gB_ProfileOptions);
            this.tabProfiles.Controls.Add(this.gB_6axis);
            this.tabProfiles.Controls.Add(this.gB_ProfileDetails);
            this.tabProfiles.Controls.Add(this.gB_Profiles);
            this.tabProfiles.Controls.Add(this.gB_ProfileGyro);
            resources.ApplyResources(this.tabProfiles, "tabProfiles");
            this.tabProfiles.Name = "tabProfiles";
            this.tabProfiles.UseVisualStyleBackColor = true;
            // 
            // gB_ProfileOptions
            // 
            this.gB_ProfileOptions.Controls.Add(this.cB_UniversalMC);
            this.gB_ProfileOptions.Controls.Add(this.lb_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.lb_UniversalMC);
            this.gB_ProfileOptions.Controls.Add(this.lb_Whitelist);
            this.gB_ProfileOptions.Controls.Add(this.cB_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.cB_Whitelist);
            resources.ApplyResources(this.gB_ProfileOptions, "gB_ProfileOptions");
            this.gB_ProfileOptions.Name = "gB_ProfileOptions";
            this.gB_ProfileOptions.TabStop = false;
            // 
            // cB_UniversalMC
            // 
            resources.ApplyResources(this.cB_UniversalMC, "cB_UniversalMC");
            this.cB_UniversalMC.Name = "cB_UniversalMC";
            this.cB_UniversalMC.UseVisualStyleBackColor = true;
            this.cB_UniversalMC.CheckedChanged += new System.EventHandler(this.cB_UniversalMC_CheckedChanged);
            // 
            // lb_Wrapper
            // 
            resources.ApplyResources(this.lb_Wrapper, "lb_Wrapper");
            this.lb_Wrapper.Name = "lb_Wrapper";
            // 
            // lb_UniversalMC
            // 
            resources.ApplyResources(this.lb_UniversalMC, "lb_UniversalMC");
            this.lb_UniversalMC.Name = "lb_UniversalMC";
            // 
            // lb_Whitelist
            // 
            resources.ApplyResources(this.lb_Whitelist, "lb_Whitelist");
            this.lb_Whitelist.Name = "lb_Whitelist";
            // 
            // cB_Wrapper
            // 
            resources.ApplyResources(this.cB_Wrapper, "cB_Wrapper");
            this.cB_Wrapper.Name = "cB_Wrapper";
            this.cB_Wrapper.UseVisualStyleBackColor = true;
            // 
            // cB_Whitelist
            // 
            resources.ApplyResources(this.cB_Whitelist, "cB_Whitelist");
            this.cB_Whitelist.Name = "cB_Whitelist";
            this.cB_Whitelist.UseVisualStyleBackColor = true;
            this.cB_Whitelist.CheckedChanged += new System.EventHandler(this.cB_Whitelist_CheckedChanged);
            // 
            // gB_6axis
            // 
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
            resources.ApplyResources(this.gB_6axis, "gB_6axis");
            this.gB_6axis.Name = "gB_6axis";
            this.gB_6axis.TabStop = false;
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
            // 
            // lB_InvertHAxis
            // 
            resources.ApplyResources(this.lB_InvertHAxis, "lB_InvertHAxis");
            this.lB_InvertHAxis.Name = "lB_InvertHAxis";
            // 
            // lB_GyroSteering
            // 
            resources.ApplyResources(this.lB_GyroSteering, "lB_GyroSteering");
            this.lB_GyroSteering.Name = "lB_GyroSteering";
            // 
            // cB_GyroSteering
            // 
            this.cB_GyroSteering.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.cB_GyroSteering, "cB_GyroSteering");
            this.cB_GyroSteering.FormattingEnabled = true;
            this.cB_GyroSteering.Items.AddRange(new object[] {
            resources.GetString("cB_GyroSteering.Items"),
            resources.GetString("cB_GyroSteering.Items1")});
            this.cB_GyroSteering.Name = "cB_GyroSteering";
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
            this.tb_ProfileAcceleroValue.Value = 10;
            this.tb_ProfileAcceleroValue.ValueChanged += new System.EventHandler(this.tb_ProfileAcceleroValue_Scroll);
            // 
            // lb_ProfileGyro
            // 
            resources.ApplyResources(this.lb_ProfileGyro, "lb_ProfileGyro");
            this.lb_ProfileGyro.Name = "lb_ProfileGyro";
            // 
            // lb_ProfileAccelero
            // 
            resources.ApplyResources(this.lb_ProfileAccelero, "lb_ProfileAccelero");
            this.lb_ProfileAccelero.Name = "lb_ProfileAccelero";
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
            this.tb_ProfileGyroValue.Value = 10;
            this.tb_ProfileGyroValue.ValueChanged += new System.EventHandler(this.tb_ProfileGyroValue_Scroll);
            // 
            // gB_ProfileDetails
            // 
            this.gB_ProfileDetails.Controls.Add(this.b_ApplyProfile);
            this.gB_ProfileDetails.Controls.Add(this.b_DeleteProfile);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfileName);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfileName);
            resources.ApplyResources(this.gB_ProfileDetails, "gB_ProfileDetails");
            this.gB_ProfileDetails.Name = "gB_ProfileDetails";
            this.gB_ProfileDetails.TabStop = false;
            // 
            // b_ApplyProfile
            // 
            resources.ApplyResources(this.b_ApplyProfile, "b_ApplyProfile");
            this.b_ApplyProfile.Name = "b_ApplyProfile";
            this.b_ApplyProfile.UseVisualStyleBackColor = true;
            this.b_ApplyProfile.Click += new System.EventHandler(this.b_ApplyProfile_Click);
            // 
            // b_DeleteProfile
            // 
            resources.ApplyResources(this.b_DeleteProfile, "b_DeleteProfile");
            this.b_DeleteProfile.Name = "b_DeleteProfile";
            this.b_DeleteProfile.UseVisualStyleBackColor = true;
            this.b_DeleteProfile.Click += new System.EventHandler(this.b_DeleteProfile_Click);
            // 
            // tB_ProfilePath
            // 
            resources.ApplyResources(this.tB_ProfilePath, "tB_ProfilePath");
            this.tB_ProfilePath.Name = "tB_ProfilePath";
            this.tB_ProfilePath.ReadOnly = true;
            // 
            // lb_ProfilePath
            // 
            resources.ApplyResources(this.lb_ProfilePath, "lb_ProfilePath");
            this.lb_ProfilePath.Name = "lb_ProfilePath";
            // 
            // tB_ProfileName
            // 
            resources.ApplyResources(this.tB_ProfileName, "tB_ProfileName");
            this.tB_ProfileName.Name = "tB_ProfileName";
            this.tB_ProfileName.ReadOnly = true;
            // 
            // lb_ProfileName
            // 
            resources.ApplyResources(this.lb_ProfileName, "lb_ProfileName");
            this.lb_ProfileName.Name = "lb_ProfileName";
            // 
            // gB_Profiles
            // 
            this.gB_Profiles.Controls.Add(this.b_CreateProfile);
            this.gB_Profiles.Controls.Add(this.lB_Profiles);
            resources.ApplyResources(this.gB_Profiles, "gB_Profiles");
            this.gB_Profiles.Name = "gB_Profiles";
            this.gB_Profiles.TabStop = false;
            // 
            // b_CreateProfile
            // 
            resources.ApplyResources(this.b_CreateProfile, "b_CreateProfile");
            this.b_CreateProfile.Name = "b_CreateProfile";
            this.b_CreateProfile.UseVisualStyleBackColor = true;
            this.b_CreateProfile.Click += new System.EventHandler(this.b_CreateProfile_Click);
            // 
            // lB_Profiles
            // 
            resources.ApplyResources(this.lB_Profiles, "lB_Profiles");
            this.lB_Profiles.FormattingEnabled = true;
            this.lB_Profiles.Name = "lB_Profiles";
            this.lB_Profiles.SelectedIndexChanged += new System.EventHandler(this.lB_Profiles_SelectedIndexChanged);
            // 
            // gB_ProfileGyro
            // 
            this.gB_ProfileGyro.Controls.Add(this.tB_UMCIntensity);
            this.gB_ProfileGyro.Controls.Add(this.cB_UMCInputButton);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCInputButton);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCIntensity);
            this.gB_ProfileGyro.Controls.Add(this.tB_UMCSensivity);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCSensivity);
            this.gB_ProfileGyro.Controls.Add(this.lb_UMCInputStyle);
            this.gB_ProfileGyro.Controls.Add(this.cB_UMCInputStyle);
            resources.ApplyResources(this.gB_ProfileGyro, "gB_ProfileGyro");
            this.gB_ProfileGyro.Name = "gB_ProfileGyro";
            this.gB_ProfileGyro.TabStop = false;
            // 
            // tB_UMCIntensity
            // 
            this.tB_UMCIntensity.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_UMCIntensity.LargeChange = 2;
            resources.ApplyResources(this.tB_UMCIntensity, "tB_UMCIntensity");
            this.tB_UMCIntensity.Maximum = 20;
            this.tB_UMCIntensity.Minimum = 1;
            this.tB_UMCIntensity.Name = "tB_UMCIntensity";
            this.tB_UMCIntensity.Value = 1;
            this.tB_UMCIntensity.Scroll += new System.EventHandler(this.tB_UMCIntensity_Scroll);
            // 
            // cB_UMCInputButton
            // 
            this.cB_UMCInputButton.FormattingEnabled = true;
            resources.ApplyResources(this.cB_UMCInputButton, "cB_UMCInputButton");
            this.cB_UMCInputButton.Name = "cB_UMCInputButton";
            this.cB_UMCInputButton.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.cB_UMCInputButton.Sorted = true;
            // 
            // lb_UMCInputButton
            // 
            resources.ApplyResources(this.lb_UMCInputButton, "lb_UMCInputButton");
            this.lb_UMCInputButton.Name = "lb_UMCInputButton";
            // 
            // lb_UMCIntensity
            // 
            resources.ApplyResources(this.lb_UMCIntensity, "lb_UMCIntensity");
            this.lb_UMCIntensity.Name = "lb_UMCIntensity";
            // 
            // tB_UMCSensivity
            // 
            resources.ApplyResources(this.tB_UMCSensivity, "tB_UMCSensivity");
            this.tB_UMCSensivity.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_UMCSensivity.LargeChange = 2;
            this.tB_UMCSensivity.Maximum = 20;
            this.tB_UMCSensivity.Minimum = 1;
            this.tB_UMCSensivity.Name = "tB_UMCSensivity";
            this.tB_UMCSensivity.Value = 1;
            this.tB_UMCSensivity.Scroll += new System.EventHandler(this.tB_UMCSensivity_Scroll);
            // 
            // lb_UMCSensivity
            // 
            resources.ApplyResources(this.lb_UMCSensivity, "lb_UMCSensivity");
            this.lb_UMCSensivity.Name = "lb_UMCSensivity";
            // 
            // lb_UMCInputStyle
            // 
            resources.ApplyResources(this.lb_UMCInputStyle, "lb_UMCInputStyle");
            this.lb_UMCInputStyle.Name = "lb_UMCInputStyle";
            // 
            // cB_UMCInputStyle
            // 
            this.cB_UMCInputStyle.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_UMCInputStyle.FormattingEnabled = true;
            this.cB_UMCInputStyle.Items.AddRange(new object[] {
            resources.GetString("cB_UMCInputStyle.Items"),
            resources.GetString("cB_UMCInputStyle.Items1"),
            resources.GetString("cB_UMCInputStyle.Items2"),
            resources.GetString("cB_UMCInputStyle.Items3")});
            resources.ApplyResources(this.cB_UMCInputStyle, "cB_UMCInputStyle");
            this.cB_UMCInputStyle.Name = "cB_UMCInputStyle";
            this.cB_UMCInputStyle.SelectedIndexChanged += new System.EventHandler(this.cB_UMCInputStyle_SelectedIndexChanged);
            // 
            // tabSettings
            // 
            this.tabSettings.Controls.Add(this.gb_SettingsUDP);
            this.tabSettings.Controls.Add(this.gb_SettingsService);
            this.tabSettings.Controls.Add(this.gb_SettingsInterface);
            resources.ApplyResources(this.tabSettings, "tabSettings");
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsUDP
            // 
            this.gb_SettingsUDP.Controls.Add(this.b_UDPApply);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPPort);
            this.gb_SettingsUDP.Controls.Add(this.lb_UDPport);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPIP);
            this.gb_SettingsUDP.Controls.Add(this.cB_UDPEnable);
            resources.ApplyResources(this.gb_SettingsUDP, "gb_SettingsUDP");
            this.gb_SettingsUDP.Name = "gb_SettingsUDP";
            this.gb_SettingsUDP.TabStop = false;
            // 
            // b_UDPApply
            // 
            resources.ApplyResources(this.b_UDPApply, "b_UDPApply");
            this.b_UDPApply.Name = "b_UDPApply";
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
            // 
            // tB_UDPIP
            // 
            resources.ApplyResources(this.tB_UDPIP, "tB_UDPIP");
            this.tB_UDPIP.Name = "tB_UDPIP";
            // 
            // cB_UDPEnable
            // 
            resources.ApplyResources(this.cB_UDPEnable, "cB_UDPEnable");
            this.cB_UDPEnable.Name = "cB_UDPEnable";
            this.cB_UDPEnable.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsService
            // 
            this.gb_SettingsService.Controls.Add(this.lB_ServiceStartup);
            this.gb_SettingsService.Controls.Add(this.lB_ServiceStatus);
            this.gb_SettingsService.Controls.Add(this.cB_ServiceStartup);
            this.gb_SettingsService.Controls.Add(this.b_ServiceStop);
            this.gb_SettingsService.Controls.Add(this.b_ServiceStart);
            this.gb_SettingsService.Controls.Add(this.b_ServiceDelete);
            this.gb_SettingsService.Controls.Add(this.b_ServiceInstall);
            resources.ApplyResources(this.gb_SettingsService, "gb_SettingsService");
            this.gb_SettingsService.Name = "gb_SettingsService";
            this.gb_SettingsService.TabStop = false;
            // 
            // lB_ServiceStartup
            // 
            resources.ApplyResources(this.lB_ServiceStartup, "lB_ServiceStartup");
            this.lB_ServiceStartup.Name = "lB_ServiceStartup";
            // 
            // lB_ServiceStatus
            // 
            resources.ApplyResources(this.lB_ServiceStatus, "lB_ServiceStatus");
            this.lB_ServiceStatus.Name = "lB_ServiceStatus";
            // 
            // cB_ServiceStartup
            // 
            this.cB_ServiceStartup.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            resources.ApplyResources(this.cB_ServiceStartup, "cB_ServiceStartup");
            this.cB_ServiceStartup.FormattingEnabled = true;
            this.cB_ServiceStartup.Items.AddRange(new object[] {
            resources.GetString("cB_ServiceStartup.Items"),
            resources.GetString("cB_ServiceStartup.Items1"),
            resources.GetString("cB_ServiceStartup.Items2")});
            this.cB_ServiceStartup.Name = "cB_ServiceStartup";
            this.cB_ServiceStartup.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // b_ServiceStop
            // 
            resources.ApplyResources(this.b_ServiceStop, "b_ServiceStop");
            this.b_ServiceStop.Name = "b_ServiceStop";
            this.b_ServiceStop.UseVisualStyleBackColor = true;
            this.b_ServiceStop.Click += new System.EventHandler(this.b_ServiceStop_Click);
            // 
            // b_ServiceStart
            // 
            resources.ApplyResources(this.b_ServiceStart, "b_ServiceStart");
            this.b_ServiceStart.Name = "b_ServiceStart";
            this.b_ServiceStart.UseVisualStyleBackColor = true;
            this.b_ServiceStart.Click += new System.EventHandler(this.b_ServiceStart_Click);
            // 
            // b_ServiceDelete
            // 
            resources.ApplyResources(this.b_ServiceDelete, "b_ServiceDelete");
            this.b_ServiceDelete.Name = "b_ServiceDelete";
            this.b_ServiceDelete.UseVisualStyleBackColor = true;
            this.b_ServiceDelete.Click += new System.EventHandler(this.b_ServiceDelete_Click);
            // 
            // b_ServiceInstall
            // 
            resources.ApplyResources(this.b_ServiceInstall, "b_ServiceInstall");
            this.b_ServiceInstall.Name = "b_ServiceInstall";
            this.b_ServiceInstall.UseVisualStyleBackColor = true;
            this.b_ServiceInstall.Click += new System.EventHandler(this.b_ServiceInstall_Click);
            // 
            // gb_SettingsInterface
            // 
            this.gb_SettingsInterface.Controls.Add(this.cB_RunAtStartup);
            this.gb_SettingsInterface.Controls.Add(this.cB_CloseMinimizes);
            this.gb_SettingsInterface.Controls.Add(this.cB_StartMinimized);
            resources.ApplyResources(this.gb_SettingsInterface, "gb_SettingsInterface");
            this.gb_SettingsInterface.Name = "gb_SettingsInterface";
            this.gb_SettingsInterface.TabStop = false;
            // 
            // cB_RunAtStartup
            // 
            resources.ApplyResources(this.cB_RunAtStartup, "cB_RunAtStartup");
            this.cB_RunAtStartup.Name = "cB_RunAtStartup";
            this.cB_RunAtStartup.UseVisualStyleBackColor = true;
            this.cB_RunAtStartup.CheckedChanged += new System.EventHandler(this.cB_RunAtStartup_CheckedChanged);
            // 
            // cB_CloseMinimizes
            // 
            resources.ApplyResources(this.cB_CloseMinimizes, "cB_CloseMinimizes");
            this.cB_CloseMinimizes.Name = "cB_CloseMinimizes";
            this.cB_CloseMinimizes.UseVisualStyleBackColor = true;
            this.cB_CloseMinimizes.CheckedChanged += new System.EventHandler(this.cB_CloseMinimizes_CheckedChanged);
            // 
            // cB_StartMinimized
            // 
            resources.ApplyResources(this.cB_StartMinimized, "cB_StartMinimized");
            this.cB_StartMinimized.Name = "cB_StartMinimized";
            this.cB_StartMinimized.UseVisualStyleBackColor = true;
            this.cB_StartMinimized.CheckedChanged += new System.EventHandler(this.cB_StartMinimized_CheckedChanged);
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
    }
}

