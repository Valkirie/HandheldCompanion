
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tB_HIDrate = new System.Windows.Forms.TrackBar();
            this.label3 = new System.Windows.Forms.Label();
            this.lB_HidMode = new System.Windows.Forms.Label();
            this.cB_HIDdevice = new System.Windows.Forms.ComboBox();
            this.groupBoxDetails = new System.Windows.Forms.GroupBox();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.label2 = new System.Windows.Forms.Label();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cB_HIDcloak = new System.Windows.Forms.ComboBox();
            this.lB_HidCloak = new System.Windows.Forms.Label();
            this.tB_InstanceID = new System.Windows.Forms.TextBox();
            this.lB_InstanceID = new System.Windows.Forms.Label();
            this.groupBoxXinput = new System.Windows.Forms.GroupBox();
            this.listBoxDevices = new System.Windows.Forms.ListBox();
            this.tabProfiles = new System.Windows.Forms.TabPage();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabDevices.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_HIDrate)).BeginInit();
            this.groupBoxDetails.SuspendLayout();
            this.groupBoxXinput.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            this.groupBox2.SuspendLayout();
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
            this.tabControl1.Enabled = false;
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(772, 592);
            this.tabControl1.TabIndex = 1;
            // 
            // tabDevices
            // 
            this.tabDevices.Controls.Add(this.groupBox1);
            this.tabDevices.Controls.Add(this.groupBoxDetails);
            this.tabDevices.Controls.Add(this.groupBoxXinput);
            this.tabDevices.Location = new System.Drawing.Point(4, 24);
            this.tabDevices.Name = "tabDevices";
            this.tabDevices.Padding = new System.Windows.Forms.Padding(3);
            this.tabDevices.Size = new System.Drawing.Size(764, 564);
            this.tabDevices.TabIndex = 0;
            this.tabDevices.Text = "Devices";
            this.tabDevices.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.tB_HIDrate);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.lB_HidMode);
            this.groupBox1.Controls.Add(this.cB_HIDdevice);
            this.groupBox1.Location = new System.Drawing.Point(252, 175);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(506, 383);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "HID Details";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(352, 66);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 15);
            this.label4.TabIndex = 6;
            this.label4.Text = "10 Miliseconds";
            // 
            // tB_HIDrate
            // 
            this.tB_HIDrate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(249)))), ((int)(((byte)(249)))));
            this.tB_HIDrate.Location = new System.Drawing.Point(156, 64);
            this.tB_HIDrate.Maximum = 300;
            this.tB_HIDrate.Minimum = 5;
            this.tB_HIDrate.Name = "tB_HIDrate";
            this.tB_HIDrate.Size = new System.Drawing.Size(190, 45);
            this.tB_HIDrate.SmallChange = 5;
            this.tB_HIDrate.TabIndex = 5;
            this.tB_HIDrate.TickStyle = System.Windows.Forms.TickStyle.None;
            this.tB_HIDrate.Value = 10;
            this.tB_HIDrate.Scroll += new System.EventHandler(this.trackBar1_Scroll);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 64);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(112, 15);
            this.label3.TabIndex = 4;
            this.label3.Text = "Output rate control:";
            // 
            // lB_HidMode
            // 
            this.lB_HidMode.AutoSize = true;
            this.lB_HidMode.Location = new System.Drawing.Point(6, 32);
            this.lB_HidMode.Name = "lB_HidMode";
            this.lB_HidMode.Size = new System.Drawing.Size(101, 15);
            this.lB_HidMode.TabIndex = 2;
            this.lB_HidMode.Text = "HID device mode:";
            // 
            // cB_HIDdevice
            // 
            this.cB_HIDdevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HIDdevice.Enabled = false;
            this.cB_HIDdevice.FormattingEnabled = true;
            this.cB_HIDdevice.Location = new System.Drawing.Point(156, 29);
            this.cB_HIDdevice.Name = "cB_HIDdevice";
            this.cB_HIDdevice.Size = new System.Drawing.Size(190, 23);
            this.cB_HIDdevice.TabIndex = 3;
            // 
            // groupBoxDetails
            // 
            this.groupBoxDetails.Controls.Add(this.checkBox2);
            this.groupBoxDetails.Controls.Add(this.label2);
            this.groupBoxDetails.Controls.Add(this.checkBox1);
            this.groupBoxDetails.Controls.Add(this.label1);
            this.groupBoxDetails.Controls.Add(this.cB_HIDcloak);
            this.groupBoxDetails.Controls.Add(this.lB_HidCloak);
            this.groupBoxDetails.Controls.Add(this.tB_InstanceID);
            this.groupBoxDetails.Controls.Add(this.lB_InstanceID);
            this.groupBoxDetails.Location = new System.Drawing.Point(252, 6);
            this.groupBoxDetails.Name = "groupBoxDetails";
            this.groupBoxDetails.Size = new System.Drawing.Size(506, 163);
            this.groupBoxDetails.TabIndex = 1;
            this.groupBoxDetails.TabStop = false;
            this.groupBoxDetails.Text = "Device Details";
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Enabled = false;
            this.checkBox2.Location = new System.Drawing.Point(156, 127);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(148, 19);
            this.checkBox2.TabIndex = 9;
            this.checkBox2.Text = "Accelerometer enabled";
            this.checkBox2.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 128);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(84, 15);
            this.label2.TabIndex = 8;
            this.label2.Text = "Accelerometer";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Enabled = false;
            this.checkBox1.Location = new System.Drawing.Point(156, 95);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(127, 19);
            this.checkBox1.TabIndex = 7;
            this.checkBox1.Text = "Gyrometer enabled";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 96);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Gyrometer";
            // 
            // cB_HIDcloak
            // 
            this.cB_HIDcloak.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HIDcloak.FormattingEnabled = true;
            this.cB_HIDcloak.Items.AddRange(new object[] {
            "True",
            "False"});
            this.cB_HIDcloak.Location = new System.Drawing.Point(156, 61);
            this.cB_HIDcloak.Name = "cB_HIDcloak";
            this.cB_HIDcloak.Size = new System.Drawing.Size(81, 23);
            this.cB_HIDcloak.TabIndex = 5;
            this.cB_HIDcloak.SelectedIndexChanged += new System.EventHandler(this.cB_HIDcloak_SelectedIndexChanged);
            // 
            // lB_HidCloak
            // 
            this.lB_HidCloak.AutoSize = true;
            this.lB_HidCloak.Location = new System.Drawing.Point(6, 64);
            this.lB_HidCloak.Name = "lB_HidCloak";
            this.lB_HidCloak.Size = new System.Drawing.Size(76, 15);
            this.lB_HidCloak.TabIndex = 4;
            this.lB_HidCloak.Text = "Device cloak:";
            // 
            // tB_InstanceID
            // 
            this.tB_InstanceID.Location = new System.Drawing.Point(156, 29);
            this.tB_InstanceID.Name = "tB_InstanceID";
            this.tB_InstanceID.ReadOnly = true;
            this.tB_InstanceID.Size = new System.Drawing.Size(222, 23);
            this.tB_InstanceID.TabIndex = 1;
            this.tB_InstanceID.Text = "82b721a0-e8b0-11eb-800a-444553540000";
            // 
            // lB_InstanceID
            // 
            this.lB_InstanceID.AutoSize = true;
            this.lB_InstanceID.Location = new System.Drawing.Point(6, 32);
            this.lB_InstanceID.Name = "lB_InstanceID";
            this.lB_InstanceID.Size = new System.Drawing.Size(68, 15);
            this.lB_InstanceID.TabIndex = 0;
            this.lB_InstanceID.Text = "Instance ID:";
            // 
            // groupBoxXinput
            // 
            this.groupBoxXinput.Controls.Add(this.listBoxDevices);
            this.groupBoxXinput.Location = new System.Drawing.Point(6, 6);
            this.groupBoxXinput.Name = "groupBoxXinput";
            this.groupBoxXinput.Size = new System.Drawing.Size(240, 552);
            this.groupBoxXinput.TabIndex = 0;
            this.groupBoxXinput.TabStop = false;
            this.groupBoxXinput.Text = "Xinput Devices";
            // 
            // listBoxDevices
            // 
            this.listBoxDevices.FormattingEnabled = true;
            this.listBoxDevices.ItemHeight = 15;
            this.listBoxDevices.Location = new System.Drawing.Point(6, 32);
            this.listBoxDevices.Name = "listBoxDevices";
            this.listBoxDevices.Size = new System.Drawing.Size(228, 514);
            this.listBoxDevices.TabIndex = 0;
            this.listBoxDevices.SelectedIndexChanged += new System.EventHandler(this.listBoxDevices_SelectedIndexChanged);
            // 
            // tabProfiles
            // 
            this.tabProfiles.Location = new System.Drawing.Point(4, 24);
            this.tabProfiles.Name = "tabProfiles";
            this.tabProfiles.Size = new System.Drawing.Size(764, 564);
            this.tabProfiles.TabIndex = 2;
            this.tabProfiles.Text = "Profiles";
            this.tabProfiles.UseVisualStyleBackColor = true;
            // 
            // tabSettings
            // 
            this.tabSettings.Controls.Add(this.groupBox3);
            this.tabSettings.Controls.Add(this.groupBox2);
            this.tabSettings.Location = new System.Drawing.Point(4, 24);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(764, 564);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            this.tabSettings.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.button1);
            this.groupBox3.Controls.Add(this.numericUpDown1);
            this.groupBox3.Controls.Add(this.label5);
            this.groupBox3.Controls.Add(this.textBox1);
            this.groupBox3.Controls.Add(this.checkBox6);
            this.groupBox3.Location = new System.Drawing.Point(6, 113);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(752, 56);
            this.groupBox3.TabIndex = 4;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "UDP Server";
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Location = new System.Drawing.Point(249, 21);
            this.numericUpDown1.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(120, 23);
            this.numericUpDown1.TabIndex = 3;
            this.numericUpDown1.Value = new decimal(new int[] {
            26760,
            0,
            0,
            0});
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(214, 23);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(29, 15);
            this.label5.TabIndex = 2;
            this.label5.Text = "Port";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(108, 20);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(100, 23);
            this.textBox1.TabIndex = 1;
            this.textBox1.Text = "127.0.0.1";
            // 
            // checkBox6
            // 
            this.checkBox6.AutoSize = true;
            this.checkBox6.Location = new System.Drawing.Point(6, 22);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(96, 19);
            this.checkBox6.TabIndex = 0;
            this.checkBox6.Text = "Enable Server";
            this.checkBox6.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.checkBox3);
            this.groupBox2.Controls.Add(this.checkBox5);
            this.groupBox2.Controls.Add(this.checkBox4);
            this.groupBox2.Location = new System.Drawing.Point(6, 6);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(752, 101);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Interface";
            // 
            // checkBox3
            // 
            this.checkBox3.AutoSize = true;
            this.checkBox3.Location = new System.Drawing.Point(6, 22);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(103, 19);
            this.checkBox3.TabIndex = 0;
            this.checkBox3.Text = "Run At Startup";
            this.checkBox3.UseVisualStyleBackColor = true;
            // 
            // checkBox5
            // 
            this.checkBox5.AutoSize = true;
            this.checkBox5.Location = new System.Drawing.Point(6, 72);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(112, 19);
            this.checkBox5.TabIndex = 2;
            this.checkBox5.Text = "Close Minimizes";
            this.checkBox5.UseVisualStyleBackColor = true;
            // 
            // checkBox4
            // 
            this.checkBox4.AutoSize = true;
            this.checkBox4.Location = new System.Drawing.Point(6, 47);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(109, 19);
            this.checkBox4.TabIndex = 1;
            this.checkBox4.Text = "Start Minimized";
            this.checkBox4.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(671, 15);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 35);
            this.button1.TabIndex = 4;
            this.button1.Text = "Apply";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
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
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "ControllerHelper";
            this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
            this.Load += new System.EventHandler(this.ControllerHelper_Load);
            this.Shown += new System.EventHandler(this.ControllerHelper_Shown);
            this.Resize += new System.EventHandler(this.ControllerHelper_Resize);
            this.contextMenuStrip1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabDevices.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tB_HIDrate)).EndInit();
            this.groupBoxDetails.ResumeLayout(false);
            this.groupBoxDetails.PerformLayout();
            this.groupBoxXinput.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem quitToolStripMenuItem;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabDevices;
        private System.Windows.Forms.TabPage tabSettings;
        private System.Windows.Forms.GroupBox groupBoxXinput;
        private System.Windows.Forms.GroupBox groupBoxDetails;
        private System.Windows.Forms.ListBox listBoxDevices;
        private System.Windows.Forms.Label lB_InstanceID;
        private System.Windows.Forms.TextBox tB_InstanceID;
        private System.Windows.Forms.ComboBox cB_HIDdevice;
        private System.Windows.Forms.Label lB_HidMode;
        private System.Windows.Forms.Label lB_HidCloak;
        private System.Windows.Forms.ComboBox cB_HIDcloak;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.CheckBox checkBox2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TrackBar tB_HIDrate;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TabPage tabProfiles;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox checkBox3;
        private System.Windows.Forms.CheckBox checkBox5;
        private System.Windows.Forms.CheckBox checkBox4;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox checkBox6;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.Button button1;
    }
}

