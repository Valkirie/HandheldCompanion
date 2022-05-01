using Microsoft.Extensions.Logging;
using Nefarius.Utilities.DeviceManagement.PnP;
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
        private ILogger logger;

        private PnPDevice device;
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

        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuidMethod(out Guid hidGuid);

        private readonly Guid hidClassInterfaceGuid;

        public ControllerEx(UserIndex index, ILogger logger)
        {
            this.logger = logger;

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

        public ControllerEx(UserIndex index, ILogger logger, ref List<PnPDevice> devices) : this(index, logger)
        {
            if (ProductId is null || VendorId is null)
                return;

            for (int i = 0; i < devices.Count; i++)
            {
                device = devices[i];

                if (!device.DeviceId.Contains($"PID_{ProductId}", StringComparison.OrdinalIgnoreCase) || !device.DeviceId.Contains($"VID_{VendorId}", StringComparison.OrdinalIgnoreCase))
                    continue;

                // update HID
                deviceInstancePath = device.DeviceId;

                while (device is not null && device.DeviceId.Contains($"VID_{VendorId}"))
                {
                    // update device details
                    DeviceDesc = device.GetProperty<string>(DevicePropertyDevice.DeviceDesc);
                    Manufacturer = device.GetProperty<string>(DevicePropertyDevice.Manufacturer);

                    baseContainerDeviceInstancePath = device.DeviceId;
                    devices.Remove(device);

                    var parentId = device.GetProperty<string>(DevicePropertyDevice.Parent);

                    if (parentId.Equals(@"HTREE\ROOT\0", StringComparison.OrdinalIgnoreCase))
                        return;

                    if (parentId.Contains(@"USB\ROOT", StringComparison.OrdinalIgnoreCase))
                        return;

                    if (parentId.Contains(@"HID\", StringComparison.OrdinalIgnoreCase))
                        return;

                    if (device.InstanceId.StartsWith(@"ROOT\SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                        device.InstanceId.StartsWith(@"ROOT\USB", StringComparison.OrdinalIgnoreCase))
                    {
                        isVirtual = true;
                        return;
                    }

                    device = PnPDevice.GetDeviceByInstanceId(parentId, DeviceLocationFlags.Phantom);
                }
            }

            // dirty, device contains HID
            if (DeviceDesc is null || Manufacturer is null)
                isVirtual = true;
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
