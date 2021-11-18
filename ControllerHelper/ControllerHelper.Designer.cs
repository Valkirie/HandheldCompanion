
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
            this.groupBoxDetails = new System.Windows.Forms.GroupBox();
            this.cB_HIDcloak = new System.Windows.Forms.ComboBox();
            this.lB_HidCloak = new System.Windows.Forms.Label();
            this.cB_HIDdevice = new System.Windows.Forms.ComboBox();
            this.lB_HidMode = new System.Windows.Forms.Label();
            this.tB_InstanceID = new System.Windows.Forms.TextBox();
            this.lB_InstanceID = new System.Windows.Forms.Label();
            this.groupBoxXinput = new System.Windows.Forms.GroupBox();
            this.listBoxDevices = new System.Windows.Forms.ListBox();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.contextMenuStrip1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabDevices.SuspendLayout();
            this.groupBoxDetails.SuspendLayout();
            this.groupBoxXinput.SuspendLayout();
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
            // groupBoxDetails
            // 
            this.groupBoxDetails.Controls.Add(this.cB_HIDcloak);
            this.groupBoxDetails.Controls.Add(this.lB_HidCloak);
            this.groupBoxDetails.Controls.Add(this.cB_HIDdevice);
            this.groupBoxDetails.Controls.Add(this.lB_HidMode);
            this.groupBoxDetails.Controls.Add(this.tB_InstanceID);
            this.groupBoxDetails.Controls.Add(this.lB_InstanceID);
            this.groupBoxDetails.Location = new System.Drawing.Point(252, 6);
            this.groupBoxDetails.Name = "groupBoxDetails";
            this.groupBoxDetails.Size = new System.Drawing.Size(506, 552);
            this.groupBoxDetails.TabIndex = 1;
            this.groupBoxDetails.TabStop = false;
            this.groupBoxDetails.Text = "Device Details";
            // 
            // cB_HIDcloak
            // 
            this.cB_HIDcloak.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HIDcloak.FormattingEnabled = true;
            this.cB_HIDcloak.Items.AddRange(new object[] {
            "True",
            "False"});
            this.cB_HIDcloak.Location = new System.Drawing.Point(156, 93);
            this.cB_HIDcloak.Name = "cB_HIDcloak";
            this.cB_HIDcloak.Size = new System.Drawing.Size(72, 23);
            this.cB_HIDcloak.TabIndex = 5;
            this.cB_HIDcloak.SelectedIndexChanged += new System.EventHandler(this.cB_HIDcloak_SelectedIndexChanged);
            // 
            // lB_HidCloak
            // 
            this.lB_HidCloak.AutoSize = true;
            this.lB_HidCloak.Location = new System.Drawing.Point(6, 96);
            this.lB_HidCloak.Name = "lB_HidCloak";
            this.lB_HidCloak.Size = new System.Drawing.Size(98, 15);
            this.lB_HidCloak.TabIndex = 4;
            this.lB_HidCloak.Text = "HID device cloak:";
            // 
            // cB_HIDdevice
            // 
            this.cB_HIDdevice.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cB_HIDdevice.Enabled = false;
            this.cB_HIDdevice.FormattingEnabled = true;
            this.cB_HIDdevice.Location = new System.Drawing.Point(156, 61);
            this.cB_HIDdevice.Name = "cB_HIDdevice";
            this.cB_HIDdevice.Size = new System.Drawing.Size(190, 23);
            this.cB_HIDdevice.TabIndex = 3;
            // 
            // lB_HidMode
            // 
            this.lB_HidMode.AutoSize = true;
            this.lB_HidMode.Location = new System.Drawing.Point(6, 64);
            this.lB_HidMode.Name = "lB_HidMode";
            this.lB_HidMode.Size = new System.Drawing.Size(101, 15);
            this.lB_HidMode.TabIndex = 2;
            this.lB_HidMode.Text = "HID device mode:";
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
            // tabSettings
            // 
            this.tabSettings.Location = new System.Drawing.Point(4, 24);
            this.tabSettings.Name = "tabSettings";
            this.tabSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabSettings.Size = new System.Drawing.Size(764, 564);
            this.tabSettings.TabIndex = 1;
            this.tabSettings.Text = "Settings";
            this.tabSettings.UseVisualStyleBackColor = true;
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
            this.groupBoxDetails.ResumeLayout(false);
            this.groupBoxDetails.PerformLayout();
            this.groupBoxXinput.ResumeLayout(false);
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
    }
}

