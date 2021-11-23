
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
            this.tB_PullRate = new System.Windows.Forms.TrackBar();
            this.lb_PullRate = new System.Windows.Forms.Label();
            this.lb_HidMode = new System.Windows.Forms.Label();
            this.cB_HidMode = new System.Windows.Forms.ComboBox();
            this.gB_XinputDetails = new System.Windows.Forms.GroupBox();
            this.cB_uncloak = new System.Windows.Forms.CheckBox();
            this.cB_HIDcloak = new System.Windows.Forms.ComboBox();
            this.lb_HidCloak = new System.Windows.Forms.Label();
            this.tB_InstanceID = new System.Windows.Forms.TextBox();
            this.lb_InstanceID = new System.Windows.Forms.Label();
            this.gB_XinputDevices = new System.Windows.Forms.GroupBox();
            this.lB_Devices = new System.Windows.Forms.ListBox();
            this.tabProfiles = new System.Windows.Forms.TabPage();
            this.gB_ProfileOptions = new System.Windows.Forms.GroupBox();
            this.tb_ProfileAcceleroValue = new System.Windows.Forms.TrackBar();
            this.lb_ProfileAccelero = new System.Windows.Forms.Label();
            this.tb_ProfileGyroValue = new System.Windows.Forms.TrackBar();
            this.lb_ProfileGyro = new System.Windows.Forms.Label();
            this.lb_Wrapper = new System.Windows.Forms.Label();
            this.lb_Whitelist = new System.Windows.Forms.Label();
            this.cB_Wrapper = new System.Windows.Forms.CheckBox();
            this.cB_Whitelist = new System.Windows.Forms.CheckBox();
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
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.gb_SettingsService = new System.Windows.Forms.GroupBox();
            this.b_ServiceStop = new System.Windows.Forms.Button();
            this.b_ServiceStart = new System.Windows.Forms.Button();
            this.b_ServiceDelete = new System.Windows.Forms.Button();
            this.b_ServiceInstall = new System.Windows.Forms.Button();
            this.lb_Service_Error = new System.Windows.Forms.Label();
            this.gb_SettingsUDP = new System.Windows.Forms.GroupBox();
            this.b_UDPApply = new System.Windows.Forms.Button();
            this.tB_UDPPort = new System.Windows.Forms.NumericUpDown();
            this.lb_UDPport = new System.Windows.Forms.Label();
            this.tB_UDPIP = new System.Windows.Forms.TextBox();
            this.cB_UDPEnable = new System.Windows.Forms.CheckBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.tB_PullRate)).BeginInit();
            this.gB_XinputDetails.SuspendLayout();
            this.gB_XinputDevices.SuspendLayout();
            this.tabProfiles.SuspendLayout();
            this.gB_ProfileOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileAcceleroValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileGyroValue)).BeginInit();
            this.gB_ProfileDetails.SuspendLayout();
            this.gB_Profiles.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.gb_SettingsService.SuspendLayout();
            this.gb_SettingsUDP.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UDPPort)).BeginInit();
            this.gb_SettingsInterface.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "Controller Helper";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.notifyIcon1_DoubleClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.quitToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(98, 26);
            // 
            // quitToolStripMenuItem
            // 
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            this.quitToolStripMenuItem.Size = new System.Drawing.Size(97, 22);
            this.quitToolStripMenuItem.Text = "Quit";
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.quitToolStripMenuItem_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabDevices);
            this.tabControl1.Controls.Add(this.tabProfiles);
            this.tabControl1.Controls.Add(this.tabSettings);
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(772, 592);
            this.tabControl1.TabIndex = 1;
            // 
            // tabDevices
            // 
            this.tabDevices.Controls.Add(this.gB_DeviceDetails);
            this.tabDevices.Controls.Add(this.gB_HIDDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDetails);
            this.tabDevices.Controls.Add(this.gB_XinputDevices);
            this.tabDevices.Location = new System.Drawing.Point(4, 24);
            this.tabDevices.Name = "tabDevices";
            this.tabDevices.Padding = new System.Windows.Forms.Padding(3);
            this.tabDevices.Size = new System.Drawing.Size(764, 564);
            this.tabDevices.TabIndex = 0;
            this.tabDevices.Text = "Devices";
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
            this.gB_DeviceDetails.Location = new System.Drawing.Point(252, 132);
            this.gB_DeviceDetails.Name = "gB_DeviceDetails";
            this.gB_DeviceDetails.Size = new System.Drawing.Size(506, 176);
            this.gB_DeviceDetails.TabIndex = 3;
            this.gB_DeviceDetails.TabStop = false;
            this.gB_DeviceDetails.Text = "Device Details";
            // 
            // cB_touchpad
            // 
            this.cB_touchpad.AutoSize = true;
            this.cB_touchpad.Location = new System.Drawing.Point(156, 81);
            this.cB_touchpad.Name = "cB_touchpad";
            this.cB_touchpad.Size = new System.Drawing.Size(142, 19);
            this.cB_touchpad.TabIndex = 11;
            this.cB_touchpad.Text = "Send touchpad inputs";
            this.cB_touchpad.UseVisualStyleBackColor = true;
            this.cB_touchpad.CheckedChanged += new System.EventHandler(this.cB_touchpad_CheckedChanged);
            // 
            // lb_touchpad
            // 
            this.lb_touchpad.AutoSize = true;
            this.lb_touchpad.Location = new System.Drawing.Point(6, 82);
            this.lb_touchpad.Name = "lb_touchpad";
            this.lb_touchpad.Size = new System.Drawing.Size(62, 15);
            this.lb_touchpad.TabIndex = 10;
            this.lb_touchpad.Text = "Touchpad:";
            // 
            // lb_gyro
            // 
            this.lb_gyro.AutoSize = true;
            this.lb_gyro.Location = new System.Drawing.Point(6, 32);
            this.lb_gyro.Name = "lb_gyro";
            this.lb_gyro.Size = new System.Drawing.Size(66, 15);
            this.lb_gyro.TabIndex = 6;
            this.lb_gyro.Text = "Gyrometer:";
            // 
            // cB_accelero
            // 
            this.cB_accelero.AutoSize = true;
            this.cB_accelero.Enabled = false;
            this.cB_accelero.Location = new System.Drawing.Point(156, 56);
            this.cB_accelero.Name = "cB_accelero";
            this.cB_accelero.Size = new System.Drawing.Size(169, 19);
            this.cB_accelero.TabIndex = 9;
            this.cB_accelero.Text = "No accelerometer detected";
            this.cB_accelero.UseVisualStyleBackColor = true;
            this.cB_accelero.CheckedChanged += new System.EventHandler(this.cB_accelero_CheckedChanged);
            // 
            // cB_gyro
            // 
            this.cB_gyro.AutoSize = true;
            this.cB_gyro.Enabled = false;
            this.cB_gyro.Location = new System.Drawing.Point(156, 31);
            this.cB_gyro.Name = "cB_gyro";
            this.cB_gyro.Size = new System.Drawing.Size(149, 19);
            this.cB_gyro.TabIndex = 7;
            this.cB_gyro.Text = "No gyrometer detected";
            this.cB_gyro.UseVisualStyleBackColor = true;
            this.cB_gyro.CheckedChanged += new System.EventHandler(this.cB_gyro_CheckedChanged);
            // 
            // lb_accelero
            // 
            this.lb_accelero.AutoSize = true;
            this.lb_accelero.Location = new System.Drawing.Point(6, 57);
            this.lb_accelero.Name = "lb_accelero";
            this.lb_accelero.Size = new System.Drawing.Size(87, 15);
            this.lb_accelero.TabIndex = 8;
            this.lb_accelero.Text = "Accelerometer:";
            // 
            // gB_HIDDetails
            // 
            this.gB_HIDDetails.Controls.Add(this.tB_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_PullRate);
            this.gB_HIDDetails.Controls.Add(this.lb_HidMode);
            this.gB_HIDDetails.Controls.Add(this.cB_HidMode);
            this.gB_HIDDetails.Location = new System.Drawing.Point(252, 314);
            this.gB_HIDDetails.Name = "gB_HIDDetails";
            this.gB_HIDDetails.Size = new System.Drawing.Size(506, 244);
            this.gB_HIDDetails.TabIndex = 2;
            this.gB_HIDDetails.TabStop = false;
            this.gB_HIDDetails.Text = "HID Details";
            // 
            // tB_PullRate
            // 
            this.tB_PullRate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_PullRate.Location = new System.Drawing.Point(156, 64);
            this.tB_PullRate.Maximum = 300;
            this.tB_PullRate.Minimum = 5;
            this.tB_PullRate.Name = "tB_PullRate";
            this.tB_PullRate.Size = new System.Drawing.Size(190, 45);
            this.tB_PullRate.SmallChange = 5;
            this.tB_PullRate.TabIndex = 5;
            this.tB_PullRate.TickStyle = System.Windows.Forms.TickStyle.None;
            this.tB_PullRate.Value = 150;
            this.tB_PullRate.ValueChanged += new System.EventHandler(this.tB_PullRate_Scroll);
            // 
            // lb_PullRate
            // 
            this.lb_PullRate.AutoSize = true;
            this.lb_PullRate.Location = new System.Drawing.Point(6, 64);
            this.lb_PullRate.Name = "lb_PullRate";
            this.lb_PullRate.Size = new System.Drawing.Size(112, 15);
            this.lb_PullRate.TabIndex = 4;
            this.lb_PullRate.Text = "Output rate control:";
            // 
            // lb_HidMode
            // 
            this.lb_HidMode.AutoSize = true;
            this.lb_HidMode.Location = new System.Drawing.Point(6, 32);
            this.lb_HidMode.Name = "lb_HidMode";
            this.lb_HidMode.Size = new System.Drawing.Size(101, 15);
            this.lb_HidMode.TabIndex = 2;
            this.lb_HidMode.Text = "HID device mode:";
            // 
            // cB_HidMode
            // 
            this.cB_HidMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HidMode.Enabled = false;
            this.cB_HidMode.FormattingEnabled = true;
            this.cB_HidMode.Location = new System.Drawing.Point(156, 29);
            this.cB_HidMode.Name = "cB_HidMode";
            this.cB_HidMode.Size = new System.Drawing.Size(190, 23);
            this.cB_HidMode.TabIndex = 3;
            // 
            // gB_XinputDetails
            // 
            this.gB_XinputDetails.Controls.Add(this.cB_uncloak);
            this.gB_XinputDetails.Controls.Add(this.cB_HIDcloak);
            this.gB_XinputDetails.Controls.Add(this.lb_HidCloak);
            this.gB_XinputDetails.Controls.Add(this.tB_InstanceID);
            this.gB_XinputDetails.Controls.Add(this.lb_InstanceID);
            this.gB_XinputDetails.Location = new System.Drawing.Point(252, 6);
            this.gB_XinputDetails.Name = "gB_XinputDetails";
            this.gB_XinputDetails.Size = new System.Drawing.Size(506, 120);
            this.gB_XinputDetails.TabIndex = 1;
            this.gB_XinputDetails.TabStop = false;
            this.gB_XinputDetails.Text = "Xinput Details";
            // 
            // cB_uncloak
            // 
            this.cB_uncloak.AutoSize = true;
            this.cB_uncloak.Location = new System.Drawing.Point(156, 87);
            this.cB_uncloak.Name = "cB_uncloak";
            this.cB_uncloak.Size = new System.Drawing.Size(116, 19);
            this.cB_uncloak.TabIndex = 10;
            this.cB_uncloak.Text = "Uncloak on close";
            this.cB_uncloak.UseVisualStyleBackColor = true;
            this.cB_uncloak.CheckedChanged += new System.EventHandler(this.cB_uncloak_CheckedChanged);
            // 
            // cB_HIDcloak
            // 
            this.cB_HIDcloak.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HIDcloak.FormattingEnabled = true;
            this.cB_HIDcloak.Items.AddRange(new object[] {
            "True",
            "False"});
            this.cB_HIDcloak.Location = new System.Drawing.Point(156, 58);
            this.cB_HIDcloak.Name = "cB_HIDcloak";
            this.cB_HIDcloak.Size = new System.Drawing.Size(81, 23);
            this.cB_HIDcloak.TabIndex = 5;
            this.cB_HIDcloak.SelectedIndexChanged += new System.EventHandler(this.cB_HIDcloak_SelectedIndexChanged);
            // 
            // lb_HidCloak
            // 
            this.lb_HidCloak.AutoSize = true;
            this.lb_HidCloak.Location = new System.Drawing.Point(6, 61);
            this.lb_HidCloak.Name = "lb_HidCloak";
            this.lb_HidCloak.Size = new System.Drawing.Size(76, 15);
            this.lb_HidCloak.TabIndex = 4;
            this.lb_HidCloak.Text = "Device cloak:";
            // 
            // tB_InstanceID
            // 
            this.tB_InstanceID.Location = new System.Drawing.Point(156, 29);
            this.tB_InstanceID.Name = "tB_InstanceID";
            this.tB_InstanceID.ReadOnly = true;
            this.tB_InstanceID.Size = new System.Drawing.Size(222, 23);
            this.tB_InstanceID.TabIndex = 1;
            // 
            // lb_InstanceID
            // 
            this.lb_InstanceID.AutoSize = true;
            this.lb_InstanceID.Location = new System.Drawing.Point(6, 32);
            this.lb_InstanceID.Name = "lb_InstanceID";
            this.lb_InstanceID.Size = new System.Drawing.Size(68, 15);
            this.lb_InstanceID.TabIndex = 0;
            this.lb_InstanceID.Text = "Instance ID:";
            // 
            // gB_XinputDevices
            // 
            this.gB_XinputDevices.Controls.Add(this.lB_Devices);
            this.gB_XinputDevices.Location = new System.Drawing.Point(6, 6);
            this.gB_XinputDevices.Name = "gB_XinputDevices";
            this.gB_XinputDevices.Size = new System.Drawing.Size(240, 552);
            this.gB_XinputDevices.TabIndex = 0;
            this.gB_XinputDevices.TabStop = false;
            this.gB_XinputDevices.Text = "Xinput Devices";
            // 
            // lB_Devices
            // 
            this.lB_Devices.FormattingEnabled = true;
            this.lB_Devices.ItemHeight = 15;
            this.lB_Devices.Location = new System.Drawing.Point(6, 32);
            this.lB_Devices.Name = "lB_Devices";
            this.lB_Devices.Size = new System.Drawing.Size(228, 514);
            this.lB_Devices.TabIndex = 0;
            this.lB_Devices.SelectedIndexChanged += new System.EventHandler(this.lB_Devices_SelectedIndexChanged);
            // 
            // tabProfiles
            // 
            this.tabProfiles.Controls.Add(this.gB_ProfileOptions);
            this.tabProfiles.Controls.Add(this.gB_ProfileDetails);
            this.tabProfiles.Controls.Add(this.gB_Profiles);
            this.tabProfiles.Location = new System.Drawing.Point(4, 24);
            this.tabProfiles.Name = "tabProfiles";
            this.tabProfiles.Size = new System.Drawing.Size(764, 564);
            this.tabProfiles.TabIndex = 2;
            this.tabProfiles.Text = "Profiles";
            this.tabProfiles.UseVisualStyleBackColor = true;
            // 
            // gB_ProfileOptions
            // 
            this.gB_ProfileOptions.Controls.Add(this.tb_ProfileAcceleroValue);
            this.gB_ProfileOptions.Controls.Add(this.lb_ProfileAccelero);
            this.gB_ProfileOptions.Controls.Add(this.tb_ProfileGyroValue);
            this.gB_ProfileOptions.Controls.Add(this.lb_ProfileGyro);
            this.gB_ProfileOptions.Controls.Add(this.lb_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.lb_Whitelist);
            this.gB_ProfileOptions.Controls.Add(this.cB_Wrapper);
            this.gB_ProfileOptions.Controls.Add(this.cB_Whitelist);
            this.gB_ProfileOptions.Location = new System.Drawing.Point(252, 135);
            this.gB_ProfileOptions.Name = "gB_ProfileOptions";
            this.gB_ProfileOptions.Size = new System.Drawing.Size(506, 423);
            this.gB_ProfileOptions.TabIndex = 2;
            this.gB_ProfileOptions.TabStop = false;
            this.gB_ProfileOptions.Text = "Profile Options";
            // 
            // tb_ProfileAcceleroValue
            // 
            this.tb_ProfileAcceleroValue.AutoSize = false;
            this.tb_ProfileAcceleroValue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tb_ProfileAcceleroValue.Location = new System.Drawing.Point(156, 125);
            this.tb_ProfileAcceleroValue.Maximum = 20;
            this.tb_ProfileAcceleroValue.Minimum = 1;
            this.tb_ProfileAcceleroValue.Name = "tb_ProfileAcceleroValue";
            this.tb_ProfileAcceleroValue.Size = new System.Drawing.Size(243, 25);
            this.tb_ProfileAcceleroValue.TabIndex = 15;
            this.tb_ProfileAcceleroValue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.tb_ProfileAcceleroValue.Value = 1;
            this.tb_ProfileAcceleroValue.ValueChanged += new System.EventHandler(this.tb_ProfileAcceleroValue_Scroll);
            // 
            // lb_ProfileAccelero
            // 
            this.lb_ProfileAccelero.AutoSize = true;
            this.lb_ProfileAccelero.Location = new System.Drawing.Point(6, 125);
            this.lb_ProfileAccelero.Name = "lb_ProfileAccelero";
            this.lb_ProfileAccelero.Size = new System.Drawing.Size(141, 15);
            this.lb_ProfileAccelero.TabIndex = 14;
            this.lb_ProfileAccelero.Text = "Accelerometer multiplier:";
            // 
            // tb_ProfileGyroValue
            // 
            this.tb_ProfileGyroValue.AutoSize = false;
            this.tb_ProfileGyroValue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tb_ProfileGyroValue.Location = new System.Drawing.Point(156, 94);
            this.tb_ProfileGyroValue.Maximum = 20;
            this.tb_ProfileGyroValue.Minimum = 1;
            this.tb_ProfileGyroValue.Name = "tb_ProfileGyroValue";
            this.tb_ProfileGyroValue.Size = new System.Drawing.Size(243, 25);
            this.tb_ProfileGyroValue.TabIndex = 13;
            this.tb_ProfileGyroValue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.tb_ProfileGyroValue.Value = 1;
            this.tb_ProfileGyroValue.ValueChanged += new System.EventHandler(this.tb_ProfileGyroValue_Scroll);
            // 
            // lb_ProfileGyro
            // 
            this.lb_ProfileGyro.AutoSize = true;
            this.lb_ProfileGyro.Location = new System.Drawing.Point(6, 94);
            this.lb_ProfileGyro.Name = "lb_ProfileGyro";
            this.lb_ProfileGyro.Size = new System.Drawing.Size(120, 15);
            this.lb_ProfileGyro.TabIndex = 12;
            this.lb_ProfileGyro.Text = "Gyrometer multiplier:";
            // 
            // lb_Wrapper
            // 
            this.lb_Wrapper.AutoSize = true;
            this.lb_Wrapper.Location = new System.Drawing.Point(6, 55);
            this.lb_Wrapper.Name = "lb_Wrapper";
            this.lb_Wrapper.Size = new System.Drawing.Size(77, 15);
            this.lb_Wrapper.TabIndex = 10;
            this.lb_Wrapper.Text = "Use Wrapper:";
            // 
            // lb_Whitelist
            // 
            this.lb_Whitelist.AutoSize = true;
            this.lb_Whitelist.Location = new System.Drawing.Point(6, 30);
            this.lb_Whitelist.Name = "lb_Whitelist";
            this.lb_Whitelist.Size = new System.Drawing.Size(69, 15);
            this.lb_Whitelist.TabIndex = 8;
            this.lb_Whitelist.Text = "Whitelisted:";
            // 
            // cB_Wrapper
            // 
            this.cB_Wrapper.AutoSize = true;
            this.cB_Wrapper.Location = new System.Drawing.Point(156, 54);
            this.cB_Wrapper.Name = "cB_Wrapper";
            this.cB_Wrapper.Size = new System.Drawing.Size(243, 19);
            this.cB_Wrapper.TabIndex = 11;
            this.cB_Wrapper.Text = "Translates XInput calls to DirectInput calls";
            this.cB_Wrapper.UseVisualStyleBackColor = true;
            // 
            // cB_Whitelist
            // 
            this.cB_Whitelist.AutoSize = true;
            this.cB_Whitelist.Location = new System.Drawing.Point(156, 29);
            this.cB_Whitelist.Name = "cB_Whitelist";
            this.cB_Whitelist.Size = new System.Drawing.Size(184, 19);
            this.cB_Whitelist.TabIndex = 9;
            this.cB_Whitelist.Text = "Can access physical controller";
            this.cB_Whitelist.UseVisualStyleBackColor = true;
            // 
            // gB_ProfileDetails
            // 
            this.gB_ProfileDetails.Controls.Add(this.b_ApplyProfile);
            this.gB_ProfileDetails.Controls.Add(this.b_DeleteProfile);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfilePath);
            this.gB_ProfileDetails.Controls.Add(this.tB_ProfileName);
            this.gB_ProfileDetails.Controls.Add(this.lb_ProfileName);
            this.gB_ProfileDetails.Location = new System.Drawing.Point(252, 6);
            this.gB_ProfileDetails.Name = "gB_ProfileDetails";
            this.gB_ProfileDetails.Size = new System.Drawing.Size(506, 123);
            this.gB_ProfileDetails.TabIndex = 1;
            this.gB_ProfileDetails.TabStop = false;
            this.gB_ProfileDetails.Text = "Profile Details";
            // 
            // b_ApplyProfile
            // 
            this.b_ApplyProfile.Location = new System.Drawing.Point(266, 87);
            this.b_ApplyProfile.Name = "b_ApplyProfile";
            this.b_ApplyProfile.Size = new System.Drawing.Size(75, 23);
            this.b_ApplyProfile.TabIndex = 7;
            this.b_ApplyProfile.Text = "Apply";
            this.b_ApplyProfile.UseVisualStyleBackColor = true;
            this.b_ApplyProfile.Click += new System.EventHandler(this.b_ApplyProfile_Click);
            // 
            // b_DeleteProfile
            // 
            this.b_DeleteProfile.Location = new System.Drawing.Point(156, 87);
            this.b_DeleteProfile.Name = "b_DeleteProfile";
            this.b_DeleteProfile.Size = new System.Drawing.Size(104, 23);
            this.b_DeleteProfile.TabIndex = 6;
            this.b_DeleteProfile.Text = "Delete profile";
            this.b_DeleteProfile.UseVisualStyleBackColor = true;
            this.b_DeleteProfile.Click += new System.EventHandler(this.b_DeleteProfile_Click);
            // 
            // tB_ProfilePath
            // 
            this.tB_ProfilePath.Location = new System.Drawing.Point(156, 58);
            this.tB_ProfilePath.Name = "tB_ProfilePath";
            this.tB_ProfilePath.ReadOnly = true;
            this.tB_ProfilePath.Size = new System.Drawing.Size(344, 23);
            this.tB_ProfilePath.TabIndex = 5;
            // 
            // lb_ProfilePath
            // 
            this.lb_ProfilePath.AutoSize = true;
            this.lb_ProfilePath.Location = new System.Drawing.Point(6, 61);
            this.lb_ProfilePath.Name = "lb_ProfilePath";
            this.lb_ProfilePath.Size = new System.Drawing.Size(34, 15);
            this.lb_ProfilePath.TabIndex = 4;
            this.lb_ProfilePath.Text = "Path:";
            // 
            // tB_ProfileName
            // 
            this.tB_ProfileName.Location = new System.Drawing.Point(156, 29);
            this.tB_ProfileName.Name = "tB_ProfileName";
            this.tB_ProfileName.ReadOnly = true;
            this.tB_ProfileName.Size = new System.Drawing.Size(222, 23);
            this.tB_ProfileName.TabIndex = 3;
            // 
            // lb_ProfileName
            // 
            this.lb_ProfileName.AutoSize = true;
            this.lb_ProfileName.Location = new System.Drawing.Point(6, 32);
            this.lb_ProfileName.Name = "lb_ProfileName";
            this.lb_ProfileName.Size = new System.Drawing.Size(42, 15);
            this.lb_ProfileName.TabIndex = 2;
            this.lb_ProfileName.Text = "Name:";
            // 
            // gB_Profiles
            // 
            this.gB_Profiles.Controls.Add(this.b_CreateProfile);
            this.gB_Profiles.Controls.Add(this.lB_Profiles);
            this.gB_Profiles.Location = new System.Drawing.Point(6, 6);
            this.gB_Profiles.Name = "gB_Profiles";
            this.gB_Profiles.Size = new System.Drawing.Size(240, 552);
            this.gB_Profiles.TabIndex = 0;
            this.gB_Profiles.TabStop = false;
            this.gB_Profiles.Text = "Profiles";
            // 
            // b_CreateProfile
            // 
            this.b_CreateProfile.Location = new System.Drawing.Point(6, 522);
            this.b_CreateProfile.Name = "b_CreateProfile";
            this.b_CreateProfile.Size = new System.Drawing.Size(228, 23);
            this.b_CreateProfile.TabIndex = 1;
            this.b_CreateProfile.Text = "Create new profile";
            this.b_CreateProfile.UseVisualStyleBackColor = true;
            this.b_CreateProfile.Click += new System.EventHandler(this.b_CreateProfile_Click);
            // 
            // lB_Profiles
            // 
            this.lB_Profiles.FormattingEnabled = true;
            this.lB_Profiles.ItemHeight = 15;
            this.lB_Profiles.Location = new System.Drawing.Point(6, 32);
            this.lB_Profiles.Name = "lB_Profiles";
            this.lB_Profiles.Size = new System.Drawing.Size(228, 484);
            this.lB_Profiles.TabIndex = 0;
            this.lB_Profiles.SelectedIndexChanged += new System.EventHandler(this.lB_Profiles_SelectedIndexChanged);
            // 
            // tabSettings
            // 
            this.tabSettings.Controls.Add(this.gb_SettingsService);
            this.tabSettings.Controls.Add(this.gb_SettingsUDP);
            this.tabSettings.Controls.Add(this.gb_SettingsInterface);
            this.tabSettings.Location = new System.Drawing.Point(4, 24);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(764, 564);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            this.tabSettings.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsService
            // 
            this.gb_SettingsService.Controls.Add(this.b_ServiceStop);
            this.gb_SettingsService.Controls.Add(this.b_ServiceStart);
            this.gb_SettingsService.Controls.Add(this.b_ServiceDelete);
            this.gb_SettingsService.Controls.Add(this.b_ServiceInstall);
            this.gb_SettingsService.Controls.Add(this.lb_Service_Error);
            this.gb_SettingsService.Location = new System.Drawing.Point(6, 175);
            this.gb_SettingsService.Name = "gb_SettingsService";
            this.gb_SettingsService.Size = new System.Drawing.Size(752, 383);
            this.gb_SettingsService.TabIndex = 5;
            this.gb_SettingsService.TabStop = false;
            this.gb_SettingsService.Text = "Controller Service";
            // 
            // b_ServiceStop
            // 
            this.b_ServiceStop.Enabled = false;
            this.b_ServiceStop.Location = new System.Drawing.Point(383, 66);
            this.b_ServiceStop.Name = "b_ServiceStop";
            this.b_ServiceStop.Size = new System.Drawing.Size(363, 23);
            this.b_ServiceStop.TabIndex = 4;
            this.b_ServiceStop.Text = "Stop Controller Service";
            this.b_ServiceStop.UseVisualStyleBackColor = true;
            this.b_ServiceStop.Click += new System.EventHandler(this.b_ServiceStop_Click);
            // 
            // b_ServiceStart
            // 
            this.b_ServiceStart.Enabled = false;
            this.b_ServiceStart.Location = new System.Drawing.Point(6, 66);
            this.b_ServiceStart.Name = "b_ServiceStart";
            this.b_ServiceStart.Size = new System.Drawing.Size(363, 23);
            this.b_ServiceStart.TabIndex = 3;
            this.b_ServiceStart.Text = "Start Controller Service";
            this.b_ServiceStart.UseVisualStyleBackColor = true;
            this.b_ServiceStart.Click += new System.EventHandler(this.b_ServiceStart_Click);
            // 
            // b_ServiceDelete
            // 
            this.b_ServiceDelete.Enabled = false;
            this.b_ServiceDelete.Location = new System.Drawing.Point(383, 37);
            this.b_ServiceDelete.Name = "b_ServiceDelete";
            this.b_ServiceDelete.Size = new System.Drawing.Size(363, 23);
            this.b_ServiceDelete.TabIndex = 2;
            this.b_ServiceDelete.Text = "Delete Controller Service";
            this.b_ServiceDelete.UseVisualStyleBackColor = true;
            this.b_ServiceDelete.Click += new System.EventHandler(this.b_ServiceDelete_Click);
            // 
            // b_ServiceInstall
            // 
            this.b_ServiceInstall.Enabled = false;
            this.b_ServiceInstall.Location = new System.Drawing.Point(6, 37);
            this.b_ServiceInstall.Name = "b_ServiceInstall";
            this.b_ServiceInstall.Size = new System.Drawing.Size(363, 23);
            this.b_ServiceInstall.TabIndex = 1;
            this.b_ServiceInstall.Text = "Install Controller Service";
            this.b_ServiceInstall.UseVisualStyleBackColor = true;
            this.b_ServiceInstall.Click += new System.EventHandler(this.b_ServiceInstall_Click);
            // 
            // lb_Service_Error
            // 
            this.lb_Service_Error.Dock = System.Windows.Forms.DockStyle.Top;
            this.lb_Service_Error.Location = new System.Drawing.Point(3, 19);
            this.lb_Service_Error.Name = "lb_Service_Error";
            this.lb_Service_Error.Size = new System.Drawing.Size(746, 15);
            this.lb_Service_Error.TabIndex = 0;
            this.lb_Service_Error.Text = "Run this tool as Administrator to unlock these settings.";
            this.lb_Service_Error.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lb_Service_Error.Visible = false;
            // 
            // gb_SettingsUDP
            // 
            this.gb_SettingsUDP.Controls.Add(this.b_UDPApply);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPPort);
            this.gb_SettingsUDP.Controls.Add(this.lb_UDPport);
            this.gb_SettingsUDP.Controls.Add(this.tB_UDPIP);
            this.gb_SettingsUDP.Controls.Add(this.cB_UDPEnable);
            this.gb_SettingsUDP.Location = new System.Drawing.Point(6, 113);
            this.gb_SettingsUDP.Name = "gb_SettingsUDP";
            this.gb_SettingsUDP.Size = new System.Drawing.Size(752, 56);
            this.gb_SettingsUDP.TabIndex = 4;
            this.gb_SettingsUDP.TabStop = false;
            this.gb_SettingsUDP.Text = "UDP Server";
            // 
            // b_UDPApply
            // 
            this.b_UDPApply.Location = new System.Drawing.Point(671, 15);
            this.b_UDPApply.Name = "b_UDPApply";
            this.b_UDPApply.Size = new System.Drawing.Size(75, 35);
            this.b_UDPApply.TabIndex = 4;
            this.b_UDPApply.Text = "Apply";
            this.b_UDPApply.UseVisualStyleBackColor = true;
            this.b_UDPApply.Click += new System.EventHandler(this.b_UDPApply_Click);
            // 
            // tB_UDPPort
            // 
            this.tB_UDPPort.Location = new System.Drawing.Point(249, 21);
            this.tB_UDPPort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.tB_UDPPort.Name = "tB_UDPPort";
            this.tB_UDPPort.Size = new System.Drawing.Size(120, 23);
            this.tB_UDPPort.TabIndex = 3;
            this.tB_UDPPort.Value = new decimal(new int[] {
            26760,
            0,
            0,
            0});
            // 
            // lb_UDPport
            // 
            this.lb_UDPport.AutoSize = true;
            this.lb_UDPport.Location = new System.Drawing.Point(214, 23);
            this.lb_UDPport.Name = "lb_UDPport";
            this.lb_UDPport.Size = new System.Drawing.Size(29, 15);
            this.lb_UDPport.TabIndex = 2;
            this.lb_UDPport.Text = "Port";
            // 
            // tB_UDPIP
            // 
            this.tB_UDPIP.Location = new System.Drawing.Point(108, 20);
            this.tB_UDPIP.Name = "tB_UDPIP";
            this.tB_UDPIP.Size = new System.Drawing.Size(100, 23);
            this.tB_UDPIP.TabIndex = 1;
            this.tB_UDPIP.Text = "127.0.0.1";
            // 
            // cB_UDPEnable
            // 
            this.cB_UDPEnable.AutoSize = true;
            this.cB_UDPEnable.Location = new System.Drawing.Point(6, 22);
            this.cB_UDPEnable.Name = "cB_UDPEnable";
            this.cB_UDPEnable.Size = new System.Drawing.Size(96, 19);
            this.cB_UDPEnable.TabIndex = 0;
            this.cB_UDPEnable.Text = "Enable Server";
            this.cB_UDPEnable.UseVisualStyleBackColor = true;
            // 
            // gb_SettingsInterface
            // 
            this.gb_SettingsInterface.Controls.Add(this.cB_RunAtStartup);
            this.gb_SettingsInterface.Controls.Add(this.cB_CloseMinimizes);
            this.gb_SettingsInterface.Controls.Add(this.cB_StartMinimized);
            this.gb_SettingsInterface.Location = new System.Drawing.Point(6, 6);
            this.gb_SettingsInterface.Name = "gb_SettingsInterface";
            this.gb_SettingsInterface.Size = new System.Drawing.Size(752, 101);
            this.gb_SettingsInterface.TabIndex = 3;
            this.gb_SettingsInterface.TabStop = false;
            this.gb_SettingsInterface.Text = "Interface";
            // 
            // cB_RunAtStartup
            // 
            this.cB_RunAtStartup.AutoSize = true;
            this.cB_RunAtStartup.Location = new System.Drawing.Point(6, 22);
            this.cB_RunAtStartup.Name = "cB_RunAtStartup";
            this.cB_RunAtStartup.Size = new System.Drawing.Size(103, 19);
            this.cB_RunAtStartup.TabIndex = 0;
            this.cB_RunAtStartup.Text = "Run At Startup";
            this.cB_RunAtStartup.UseVisualStyleBackColor = true;
            this.cB_RunAtStartup.CheckedChanged += new System.EventHandler(this.cB_RunAtStartup_CheckedChanged);
            // 
            // cB_CloseMinimizes
            // 
            this.cB_CloseMinimizes.AutoSize = true;
            this.cB_CloseMinimizes.Location = new System.Drawing.Point(6, 72);
            this.cB_CloseMinimizes.Name = "cB_CloseMinimizes";
            this.cB_CloseMinimizes.Size = new System.Drawing.Size(112, 19);
            this.cB_CloseMinimizes.TabIndex = 2;
            this.cB_CloseMinimizes.Text = "Close Minimizes";
            this.cB_CloseMinimizes.UseVisualStyleBackColor = true;
            this.cB_CloseMinimizes.CheckedChanged += new System.EventHandler(this.cB_CloseMinimizes_CheckedChanged);
            // 
            // cB_StartMinimized
            // 
            this.cB_StartMinimized.AutoSize = true;
            this.cB_StartMinimized.Location = new System.Drawing.Point(6, 47);
            this.cB_StartMinimized.Name = "cB_StartMinimized";
            this.cB_StartMinimized.Size = new System.Drawing.Size(109, 19);
            this.cB_StartMinimized.TabIndex = 1;
            this.cB_StartMinimized.Text = "Start Minimized";
            this.cB_StartMinimized.UseVisualStyleBackColor = true;
            this.cB_StartMinimized.CheckedChanged += new System.EventHandler(this.cB_StartMinimized_CheckedChanged);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // toolTip1
            // 
            this.toolTip1.AutomaticDelay = 100;
            // 
            // ControllerHelper
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(796, 616);
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "ControllerHelper";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "ControllerHelper";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ControllerHelper_Close);
            this.Load += new System.EventHandler(this.ControllerHelper_Load);
            this.Shown += new System.EventHandler(this.ControllerHelper_Shown);
            this.Resize += new System.EventHandler(this.ControllerHelper_Resize);
            this.contextMenuStrip1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabDevices.ResumeLayout(false);
            this.gB_DeviceDetails.ResumeLayout(false);
            this.gB_DeviceDetails.PerformLayout();
            this.gB_HIDDetails.ResumeLayout(false);
            this.gB_HIDDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_PullRate)).EndInit();
            this.gB_XinputDetails.ResumeLayout(false);
            this.gB_XinputDetails.PerformLayout();
            this.gB_XinputDevices.ResumeLayout(false);
            this.tabProfiles.ResumeLayout(false);
            this.gB_ProfileOptions.ResumeLayout(false);
            this.gB_ProfileOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileAcceleroValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tb_ProfileGyroValue)).EndInit();
            this.gB_ProfileDetails.ResumeLayout(false);
            this.gB_ProfileDetails.PerformLayout();
            this.gB_Profiles.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.gb_SettingsService.ResumeLayout(false);
            this.gb_SettingsUDP.ResumeLayout(false);
            this.gb_SettingsUDP.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_UDPPort)).EndInit();
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
        private System.Windows.Forms.ComboBox cB_HIDcloak;
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
        private System.Windows.Forms.Label lb_Service_Error;
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
    }
}

