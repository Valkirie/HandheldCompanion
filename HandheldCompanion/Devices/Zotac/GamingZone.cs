using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.Zotac
{
    public class GamingZone : IDevice
    {
        private bool IsReadingInput = false;
        private bool IsReadingDialWheel = false;

        private const byte INPUT_HID_ID = 0x00;
        private const byte DIALWHEEL_HID_ID = 0x03;

        public GamingZone()
        {
            // device specific settings
            this.ProductIllustration = "device_zotac_zone";
            
            // used to monitor OEM specific inputs
            vendorId = 0x1EE9;
            productIds = [0x1590];

            // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
            // https://www.amd.com/en/products/apu/amd-ryzen-7-8840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 3, 28 };
            this.GfxClock = new double[] { 100, 2700 };
            this.CpuClock = 5100;

            this.OEMChords.Add(new KeyboardChord("ZOTAC key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                false, ButtonFlags.OEM1
            ));

            this.OEMChords.Add(new KeyboardChord("Dots key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                false, ButtonFlags.OEM2
            ));

            this.OEMChords.Add(new KeyboardChord("Home key",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.LWin, KeyCode.D],
                false, ButtonFlags.OEM3
            ));
        }

        public override void OpenEvents()
        {
            base.OpenEvents();

            // manage events
            ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

            Device_Inserted();
        }

        public override void Close()
        {
            // close devices
            lock (this.updateLock)
            {
                foreach (HidDevice hidDevice in hidDevices.Values)
                    hidDevice.Dispose();
                hidDevices.Clear();
            }

            // manage events
            ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
            ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

            base.Close();
        }

        private void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool IsPowerCycling)
        {
            if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
                Device_Inserted(true);
        }

        private void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            // hack, force rescan
            if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
                Device_Removed();
        }

        private void Device_Removed()
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.MonitorDeviceEvents = false;
                device.Removed -= Device_Removed;
                try { device.Dispose(); } catch { }

                // stop further reads
                IsReadingInput = false;
            }

            if (hidDevices.TryGetValue(DIALWHEEL_HID_ID, out device))
            {
                device.MonitorDeviceEvents = false;
                device.Removed -= Device_Removed;
                try { device.Dispose(); } catch { }

                // stop further reads
                IsReadingDialWheel = false;
            }
        }

        private async void Device_Inserted(bool reScan = false)
        {
            // if you still want to automatically re-attach:
            if (reScan)
                await WaitUntilReady();

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.MonitorDeviceEvents = true;
                device.Removed += Device_Removed;
                device.OpenDevice();

                // fire‐and‐forget the read loop
                IsReadingInput = true;
                _ = ReadInputLoopAsync(device);
            }

            if (hidDevices.TryGetValue(DIALWHEEL_HID_ID, out device))
            {
                device.MonitorDeviceEvents = true;
                device.Removed += Device_Removed;
                device.OpenDevice();

                // fire‐and‐forget the read loop
                IsReadingDialWheel = true;
                _ = ReadDialWheelLoopAsync(device);
            }
        }

        private async Task ReadInputLoopAsync(HidDevice device)
        {
            try
            {
                while (IsReadingInput)
                {
                    HidReport report = await device.ReadReportAsync().ConfigureAwait(false);
                    // do something
                }
            }
            catch { }
        }

        private async Task ReadDialWheelLoopAsync(HidDevice device)
        {
            try
            {
                while (IsReadingDialWheel)
                {
                    HidReport report = await device.ReadReportAsync().ConfigureAwait(false);
                    // do something
                }
            }
            catch { }
        }

        public override bool IsReady()
        {
            IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                if (device.ReadFeatureData(out byte[] data, INPUT_HID_ID))
                    hidDevices[INPUT_HID_ID] = device;
                else if (device.ReadFeatureData(out data, DIALWHEEL_HID_ID))
                    hidDevices[DIALWHEEL_HID_ID] = device;
            }

            return false;
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                    return "\u221D";
                case ButtonFlags.OEM2:
                    return "\u221E";
                case ButtonFlags.OEM3:
                    return "\u21F9";
            }

            return base.GetGlyph(button);
        }
    }
}
