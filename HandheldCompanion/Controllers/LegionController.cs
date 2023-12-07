using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;

namespace HandheldCompanion.Controllers
{
    public class LegionController : XInputController
    {
        // Import the user32.dll library
        [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

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
        private const byte FRONT_IDX = 17;
        private const byte BACK_IDX = 19;
        private const byte STATUS_IDX = 0;
        private const byte PING_IDX = 40;

        private Thread dataThread;
        private bool dataThreadRunning;

        private byte[] Data = new byte[64];
        public override bool IsReady
        {
            get
            {
                byte status = GetStatus(STATUS_IDX);
                return status == 25;
            }
        }

        public bool IsWireless
        {
            get
            {
                byte status = GetStatus(PING_IDX);
                return (status >= 40 && status <= 50);
            }
        }

        // Define some constants for the touchpad logic
        private bool IsPassthrough = false;
        private uint LongPressTime = 1000; // The minimum time in milliseconds for a long press
        private const int MaxDistance = 40; // Maximum distance tolerance between touch and untouch in pixels

        // Variables to store the touchpad state
        private bool touchpadTouched = false; // Whether the touchpad is currently touched
        private Vector2 touchpadPosition = Vector2.Zero; // The current position of the touchpad
        private Vector2 touchpadFirstPosition = Vector2.Zero; // The first position of the touchpad when touched
        private long touchpadStartTime = 0; // The start time of the touchpad when touched
        private long touchpadEndTime = 0; // The end time of the touchpad when untouched
        private bool touchpadDoubleTapped = false; // Whether the touchpad has been double tapped
        private bool touchpadLongTapped = false; // Whether the touchpad has been long tapped

        private long lastTap = 0;
        private Vector2 lastTapPosition = Vector2.Zero; // The current position of the touchpad

        public LegionController(PnPDetails details) : base(details)
        {
            // Additional controller specific source buttons
            SourceButtons.Add(ButtonFlags.RightPadTouch);
            SourceButtons.Add(ButtonFlags.RightPadClick);
            SourceButtons.Add(ButtonFlags.RightPadClickDown);

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

            // get long press time from system settings
            SystemParametersInfo(0x006A, 0, ref LongPressTime, 0);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            hidDevice = GetHidDevice();
            if (hidDevice is not null)
                hidDevice.OpenDevice();
        }

        private HidDevice GetHidDevice()
        {
            IEnumerable<HidDevice> devices = IDevice.GetHidDevices(Details.VendorID, Details.ProductID, 0);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                if (device.Capabilities.InputReportByteLength == 64)
                    return device;  // HID-compliant vendor-defined device
            }

            return null;
        }

        private byte GetStatus(int idx)
        {
            if (hidDevice is not null)
            {
                HidReport report = hidDevice.ReadReport();
                if (report.Data is not null)
                    return report.Data[idx];
            }

            return 0;
        }

        public override void Plug()
        {
            hidDevice = GetHidDevice();

            if (hidDevice is not null && hidDevice.IsConnected)
            {
                if (!hidDevice.IsOpen)
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
                if (dataThread is not null)
                    dataThread.Join();

                if (hidDevice.IsConnected && hidDevice.IsOpen)
                {
                    hidDevice.CloseDevice();
                    hidDevice.Dispose();
                    hidDevice = null;
                }
            }

            base.Unplug();
        }

        public override void UpdateInputs(long ticks, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            base.UpdateInputs(ticks, false);

            FrontEnum frontButton = (FrontEnum)Data[FRONT_IDX];
            Inputs.ButtonState[ButtonFlags.OEM1] = frontButton.HasFlag(FrontEnum.LegionR);
            Inputs.ButtonState[ButtonFlags.OEM2] = frontButton.HasFlag(FrontEnum.LegionL);

            BackEnum backButton = (BackEnum)Data[BACK_IDX];
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

            Inputs.ButtonState[ButtonFlags.RightPadTouch] = touched;
            Inputs.ButtonState[ButtonFlags.RightPadClick] = false;
            Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;

            // handle touchpad if passthrough is off
            if (!IsPassthrough)
                HandleTouchpadInput(touched, TouchpadX, TouchpadY);

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
                if (hidDevice is null)
                    continue;

                HidReport report = hidDevice.ReadReport();
                if (report is not null)
                {
                    // check if packet is safe
                    if (report.Data[STATUS_IDX] == 25)
                        Data = report.Data;
                }
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

        public void HandleTouchpadInput(bool touched, ushort x, ushort y)
        {
            // Convert the ushort values to Vector2
            Vector2 position = new Vector2(x, y);

            // If the touchpad is touched
            if (touched)
            {
                Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange((short)x, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange((short)-y, 0, 1000, short.MinValue, short.MaxValue);

                // If the touchpad was not touched before
                if (!touchpadTouched)
                {
                    // Set the touchpad state variables
                    touchpadTouched = true;
                    touchpadPosition = position;
                    touchpadFirstPosition = position;
                    touchpadStartTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    // Set the right pad touch flag to true
                    Inputs.ButtonState[ButtonFlags.RightPadTouch] = true;

                    long delay = touchpadStartTime - lastTap;
                    float distance = Vector2.Distance(touchpadFirstPosition, lastTapPosition);

                    if (delay < SystemInformation.DoubleClickTime && distance < MaxDistance * 5)
                    {
                        Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
                        touchpadDoubleTapped = true;
                    }
                }
                // If the touchpad was touched before
                else
                {
                    // Update the touchpad position
                    touchpadPosition = position;

                    // If the touchpad has been double tapped
                    if (touchpadDoubleTapped)
                    {
                        // Keep the right pad click flag to true
                        Inputs.ButtonState[ButtonFlags.RightPadClick] = true;
                    }
                    else
                    {
                        // Calculate the duration and the distance of the touchpad
                        long duration = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - touchpadStartTime;
                        float distance = Vector2.Distance(touchpadFirstPosition, touchpadPosition);

                        // If the duration is more than the long tap duration and the distance is less than the maximum distance
                        if (duration >= LongPressTime && duration < (LongPressTime + 100) && distance < MaxDistance)
                        {
                            // If the touchpad has not been long tapped before
                            if (!touchpadLongTapped)
                            {
                                // Set the right pad click down flag to true
                                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = true;

                                // Set the touchpad long tapped flag to true
                                touchpadLongTapped = true;
                            }
                        }
                    }
                }
            }
            // If the touchpad is not touched
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;

                // If the touchpad was touched before
                if (touchpadTouched)
                {
                    // Set the touchpad state variables
                    touchpadTouched = false;
                    touchpadEndTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    // Set the right pad touch flag to false
                    Inputs.ButtonState[ButtonFlags.RightPadTouch] = false;

                    // Calculate the duration and the distance of the touchpad
                    long duration = touchpadEndTime - touchpadStartTime;
                    float distance = Vector2.Distance(touchpadFirstPosition, touchpadPosition);

                    // If the duration is less than the short tap duration and the distance is less than the maximum distance
                    if (duration < SystemInformation.DoubleClickTime && distance < MaxDistance)
                    {
                        // Set the right pad click flag to true
                        Inputs.ButtonState[ButtonFlags.RightPadClick] = true;

                        // Store tap time
                        lastTap = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                        lastTapPosition = touchpadPosition;
                    }

                    // Set the touchpad long tapped flag to false
                    touchpadLongTapped = false;

                    // Set the touchpad double tapped flag to false
                    touchpadDoubleTapped = false;
                }
            }
        }

        internal void SetPassthrough(bool enabled)
        {
            switch(enabled)
            {
                case true:
                    SetTouchPadStatus(1);
                    break;
                case false:
                    SetTouchPadStatus(0);
                    break;
            }

            IsPassthrough = enabled;
        }
    }
}
