using Nefarius.Utilities.DeviceManagement.PnP;
using PInvoke;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Timers;
using static ControllerCommon.Utils.DeviceUtils;

namespace ControllerCommon
{
    public class ControllerEx
    {
        private PnPDetails Details;
        public Controller Controller;
        public string Manufacturer, DeviceDesc;
        public UserIndex UserIndex;

        private XInputCapabilitiesEx CapabilitiesEx;

        public bool isVirtual;
        public string ProductId, VendorId, XID;

        public string deviceInstancePath = "";
        public string baseContainerDeviceInstancePath = "";

        private Vibration IdentifyVibration = new Vibration() { LeftMotorSpeed = ushort.MaxValue, RightMotorSpeed = ushort.MaxValue };
        private Timer IdentifyTimer;

        public ControllerEx(UserIndex index)
        {
            this.Controller = new Controller(index);
            this.UserIndex = index;

            // initialize timers
            IdentifyTimer = new Timer(200) { AutoReset = false };
            IdentifyTimer.Elapsed += IdentifyTimer_Tick;

            // pull data from xinput
            CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, (int)UserIndex, 0, ref CapabilitiesEx) == 0)
            {
                ProductId = CapabilitiesEx.ProductId.ToString("X4");
                VendorId = CapabilitiesEx.VendorId.ToString("X4");
            }
        }

        public ControllerEx(UserIndex index, ref List<PnPDetails> devices) : this(index)
        {
            if (ProductId is null || VendorId is null)
                return;

            foreach (PnPDetails deviceEx in devices)
            {
                if (deviceEx.attributes.ProductID != CapabilitiesEx.ProductId || deviceEx.attributes.VendorID != CapabilitiesEx.VendorId)
                    continue;

                // update current device
                this.Details = deviceEx;
                isVirtual = deviceEx.isVirtual;

                // update HID
                deviceInstancePath = deviceEx.deviceInstancePath;
                baseContainerDeviceInstancePath = deviceEx.baseContainerDeviceInstancePath;

                DeviceDesc = deviceEx.DeviceDesc;
                Manufacturer = deviceEx.Manufacturer;

                devices.Remove(deviceEx);
                break;
            }
        }

        public override string ToString()
        {
            return DeviceDesc;
        }

        public State GetState()
        {
            return Controller.GetState();
        }

        public bool IsConnected()
        {
            return Controller.IsConnected;
        }

        public ushort GetPID()
        {
            return CapabilitiesEx.ProductId;
        }

        public ushort GetVID()
        {
            return CapabilitiesEx.VendorId;
        }

        public void Identify()
        {
            if (!Controller.IsConnected)
                return;

            Controller.SetVibration(IdentifyVibration);
            IdentifyTimer.Stop();
            IdentifyTimer.Start();
        }

        private void IdentifyTimer_Tick(object sender, EventArgs e)
        {
            Controller.SetVibration(new Vibration());
        }
    }
}
