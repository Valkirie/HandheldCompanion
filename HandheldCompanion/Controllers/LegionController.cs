using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Lenovo;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using HidLibrary;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;

namespace HandheldCompanion.Controllers
{
    public class LegionController : XInputController
    {
        [Flags]
        private enum FrontEnum
        {
            None = 0,
            LegionR = 64,
            LegionL = 128,
        }

        [Flags]
        private enum BackEnum
        {
            None = 0,
            M3 = 4,
            M2 = 8,
            Y3 = 32,
            Y2 = 64,
            Y1 = 128,
        }

        private HidDevice hidDevice;
        private const byte FRONT_ID = 17;
        private const byte BACK_ID = 19;

        private Thread dataThread;
        private bool dataThreadRunning;

        private byte[] Data = new byte[64];
        public override bool IsReady => GetStatus() == 0 ? false : true;

        public bool IsConnected => !IsWireless;
        public bool IsWireless => GetStatus() >= 40 && GetStatus() <= 50;

        private bool prevTouch = false;
        private Vector2 prevTouchVector = Vector2.Zero;
        private Vector2 currentTouchVector = Vector2.Zero;
        private DateTime prevTouchTime = DateTime.Now;
        private const int DoubleClickWidth = 4;

        public LegionController(Controller controller, PnPDetails details) : base(controller, details)
        {
            // Additional controller specific source buttons
            SourceButtons.Add(ButtonFlags.RightPadTouch);
            SourceButtons.Add(ButtonFlags.RightPadClick);

            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.R5);
            SourceButtons.Add(ButtonFlags.L4);
            SourceButtons.Add(ButtonFlags.L5);

            SourceButtons.Add(ButtonFlags.B5);
            SourceButtons.Add(ButtonFlags.B6);
            SourceButtons.Add(ButtonFlags.B7);
            SourceButtons.Add(ButtonFlags.B8);

            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);

            hidDevice = GetHidDevice();
            if (hidDevice is not null)
                hidDevice.OpenDevice();
        }

        private HidDevice GetHidDevice()
        {
            IEnumerable<HidDevice> devices = IDevice.GetHidDevices(Details.attributes.VendorID, Details.attributes.ProductID, 0);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                if (device.Capabilities.InputReportByteLength == 64)
                    return device;  // HID-compliant vendor-defined device
            }

            return null;
        }

        private byte GetStatus()
        {
            if (hidDevice is not null)
            {
                HidReport report = hidDevice.ReadReport();
                if (report.Data is not null)
                    return report.Data[40];
            }

            return 0;
        }

        public override void Plug()
        {
            hidDevice = GetHidDevice();
            if (hidDevice is not null)
            {
                hidDevice.OpenDevice();

                dataThreadRunning = true;
                dataThread = new Thread(dataThreadLoop);
                dataThread.IsBackground = true;
                dataThread.Start();
            }

            base.Plug();
        }

        public override void Unplug()
        {
            if (hidDevice is not null)
            {
                // kill rumble thread
                dataThreadRunning = false;
                dataThread.Join();

                hidDevice.CloseDevice();
            }

            base.Unplug();
        }

        public override void UpdateInputs(long ticks, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            base.UpdateInputs(ticks, false);

            FrontEnum frontButton = (FrontEnum)Data[FRONT_ID];
            Inputs.ButtonState[ButtonFlags.OEM1] = frontButton.HasFlag(FrontEnum.LegionR);
            Inputs.ButtonState[ButtonFlags.OEM2] = frontButton.HasFlag(FrontEnum.LegionL);

            BackEnum backButton = (BackEnum)Data[BACK_ID];
            Inputs.ButtonState[ButtonFlags.R4] = backButton.HasFlag(BackEnum.M3);
            Inputs.ButtonState[ButtonFlags.R5] = backButton.HasFlag(BackEnum.Y3);
            Inputs.ButtonState[ButtonFlags.L4] = backButton.HasFlag(BackEnum.Y1);
            Inputs.ButtonState[ButtonFlags.L5] = backButton.HasFlag(BackEnum.Y2);
            Inputs.ButtonState[ButtonFlags.B5] = backButton.HasFlag(BackEnum.M2);
            Inputs.ButtonState[ButtonFlags.B6] = Data[20] == 128;   // Scroll click
            Inputs.ButtonState[ButtonFlags.B7] = Data[24] == 129;   // Scroll up
            Inputs.ButtonState[ButtonFlags.B8] = Data[24] == 255;   // Scroll down

            // Right Pad
            ushort TouchpadX = (ushort)((Data[25] << 8) | Data[26]);
            ushort TouchpadY = (ushort)((Data[27] << 8) | Data[28]);

            bool touched = (TouchpadX != 0 || TouchpadY != 0);

            Inputs.ButtonState[ButtonFlags.RightPadClick] = false;
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = false;

            if (touched)
            {
                if (!prevTouch)
                {
                    prevTouchVector = new(TouchpadX, TouchpadY);
                    prevTouchTime = DateTime.Now;
                    prevTouch = true;
                }

                Inputs.ButtonState[ButtonFlags.RightPadTouch] = true;

                Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange((short)TouchpadX, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange((short)-TouchpadY, 0, 1000, short.MinValue, short.MaxValue);

                currentTouchVector = new(TouchpadX, TouchpadY);
            }
            else if (prevTouch)
            {
                Vector2 TouchDist = currentTouchVector - prevTouchVector;
                if (TouchDist.Length() < DoubleClickWidth)
                {
                    TimeSpan TouchDiff = DateTime.Now - prevTouchTime;
                    if (TouchDiff.TotalMilliseconds < SystemInformation.DoubleClickTime)
                        Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
                }

                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;

                prevTouch = false;
            }

            /*
            Inputs.AxisState[AxisFlags.LeftStickX] += (short)InputUtils.MapRange(Data[29], byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.LeftStickY] -= (short)InputUtils.MapRange(Data[30], byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue);

            Inputs.AxisState[AxisFlags.RightStickX] += (short)InputUtils.MapRange(Data[31], byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightStickY] -= (short)InputUtils.MapRange(Data[32], byte.MinValue, byte.MaxValue, short.MinValue, short.MaxValue);
            */

            base.UpdateInputs(ticks);
        }

        private async void dataThreadLoop(object? obj)
        {
            // pull latest Data
            while (dataThreadRunning)
            {
                HidReport report = hidDevice.ReadReport();
                if (report is not null)
                    Data = report.Data;
            }
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B5:
                    return "\u2213"; // M2
                case ButtonFlags.B6:
                    return "\u2206"; // Scroll click
                case ButtonFlags.B7:
                    return "\u27F0"; // Scroll up
                case ButtonFlags.B8:
                    return "\u27F1"; // Scroll down
            }
            
            return base.GetGlyph(button);
        }
    }
}
