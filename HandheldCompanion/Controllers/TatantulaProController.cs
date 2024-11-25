using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace HandheldCompanion.Controllers
{
    public class TatantulaProController : XInputController
    {
        private HidDevice hidDevice;

        private Thread dataThread;
        private bool dataThreadRunning;
        private byte[] Data = new byte[64];

        protected float aX = 0.0f, aZ = 0.0f, aY = 0.0f;
        protected float gX = 0.0f, gZ = 0.0f, gY = 0.0f;
        private const byte EXTRABUTTON0_IDX = 11;
        private const byte EXTRABUTTON1_IDX = 12;
        private const byte EXTRABUTTON2_IDX = 13;

        [Flags]
        private enum Button0Enum
        {
            None = 0,
            M = 4
        }

        [Flags]
        private enum Button1Enum
        {
            None = 0,
            M1 = 1,
            M2 = 2,
            T1 = 4,
            T2 = 8,
            T3 = 16,
            C1 = 32,
            C2 = 64,
            C3 = 128
        }

        [Flags]
        private enum Button2Enum
        {
            None = 0,
            C4 = 1
        }

        [Flags]
        private enum ButtonLayout
        {
            Xbox = 64,
            Nintendo = 128,
        }

        public TatantulaProController() : base()
        { }

        public TatantulaProController(PnPDetails details) : base(details)
        {
            // Capabilities
            Capabilities |= ControllerCapabilities.MotionSensor;
        }

        public override string ToString()
        {
            return $"GameSir Tarantula Pro PC Controller";
        }

        protected override void InitializeInputOutput()
        {
            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.L4);
            SourceButtons.Add(ButtonFlags.L5);

            SourceButtons.Add(ButtonFlags.B5);
            SourceButtons.Add(ButtonFlags.B6);
            SourceButtons.Add(ButtonFlags.B7);
            SourceButtons.Add(ButtonFlags.B8);
            SourceButtons.Add(ButtonFlags.B9);
            SourceButtons.Add(ButtonFlags.B10);
            SourceButtons.Add(ButtonFlags.B11);

            SourceAxis.Add(AxisLayoutFlags.Gyroscope);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            hidDevice = GetHidDevice();
            hidDevice?.OpenDevice();
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

        //                                                                  (test: 0x01, normal: 0x02)
        private byte[] ControllerMode = new byte[33] { 0x7, 0x04, 0x0a, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                  brigthness  mode
        private byte[] LEDMode = new byte[33] { 0x7, 0x06, 0x07, 0x01, 0x64, 0x32, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                        H     V     S
        private byte[] LEDColor = new byte[33] { 0x7, 0x10, 0x07, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                                                      button            action            button
        private byte[] ButtonMode = new byte[33] { 0x7, 0x13, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                         T3    C1    C2    T1    T2    C3    C4    M1    M2
        private byte[] ExtraButtons = new byte[] { 0x28, 0x29, 0x2a, 0x26, 0x27, 0x2b, 0x2c, 0x24, 0x25 };

        //                                                                                 (nintendo: 0x02, xbox: 0x01)
        private byte[] ControllerLayout = new byte[] { 0x07, 0x07, 0x09, 0x01, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        private ButtonLayout GetLayout()
        {
            ButtonLayout layout = (ButtonLayout)Data[EXTRABUTTON2_IDX];
            return layout.HasFlag(ButtonLayout.Xbox) ? ButtonLayout.Xbox : ButtonLayout.Nintendo;
        }

        private void SetVerboseMode()
        {
            // unlock raw hid report
            ControllerMode[4] = 0x02;
            hidDevice.Write(ControllerMode);
        }

        private void SetTestMode()
        {
            // unlock raw hid report
            ControllerMode[4] = 0x01;
            hidDevice.Write(ControllerMode);
        }

        public override void Plug()
        {
            hidDevice = GetHidDevice();
            if (hidDevice is not null && hidDevice.IsConnected)
            {
                if (!hidDevice.IsOpen)
                    hidDevice.OpenDevice();

                // unlock raw hid report
                SetTestMode();

                foreach (byte button in ExtraButtons)
                {
                    ButtonMode[10] = button;
                    hidDevice.Write(ButtonMode);
                }

                /*
                ControllerLayout[6] = 0x02; // Nintendo
                hidDevice.Write(ControllerLayout);

                ButtonLayout layout = GetLayout();

                ControllerLayout[6] = 0x01; // XBOX
                hidDevice.Write(ControllerLayout);

                layout = GetLayout();
                */

                /*
                // LED ON
                LEDMode[6] = 1;
                hidDevice.Write(LEDMode);
                */

                // start data thread
                if (dataThread is null)
                {
                    dataThreadRunning = true;
                    dataThread = new Thread(dataThreadLoop)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Highest
                    };
                    dataThread.Start();
                }
            }

            base.Plug();
        }

        public override void Unplug()
        {
            // Kill data thread
            if (dataThread is not null)
            {
                dataThreadRunning = false;
                // Ensure the thread has finished execution
                if (dataThread.IsAlive)
                    dataThread.Join();
                dataThread = null;
            }

            if (hidDevice is not null)
            {
                if (hidDevice.IsConnected && hidDevice.IsOpen)
                    hidDevice.CloseDevice();

                hidDevice.Dispose();
                hidDevice = null;
            }

            base.Unplug();
        }

        public void ColorToHSV(Color color, out double hue, out double saturation, out double value)
        {
            // Convert RGB values to a scale of 0 to 1
            float rScaled = color.R / 255f;
            float gScaled = color.G / 255f;
            float bScaled = color.B / 255f;

            // Find the maximum and minimum values of R, G and B
            float max = Math.Max(rScaled, Math.Max(gScaled, bScaled));
            float min = Math.Min(rScaled, Math.Min(gScaled, bScaled));
            float delta = max - min;

            // Calculate V (value/brightness) - scaled to 100%
            value = max * 100;

            // Calculate S (saturation) - scaled to 100%
            saturation = (max == 0) ? 0 : (delta / max) * 100;

            // Calculate H (hue)
            hue = color.GetHue();
            hue = hue / 360.0f * 255f;
        }

        public override void SetLightColor(byte R, byte G, byte B)
        {
            if (hidDevice is null || !hidDevice.IsConnected || !hidDevice.IsOpen)
                return;

            Color color = Color.FromArgb(R, G, B);
            ColorToHSV(color, out double hue, out double saturation, out double value);

            LEDColor[5] = (byte)hue;
            LEDColor[6] = (byte)saturation;
            LEDColor[7] = (byte)value;

            hidDevice.Write(LEDColor);
        }

        public override void UpdateInputs(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            base.UpdateInputs(ticks, delta, false);

            Button0Enum extraButtons0 = (Button0Enum)Data[EXTRABUTTON0_IDX];
            Inputs.ButtonState[ButtonFlags.L5] = extraButtons0.HasFlag(Button0Enum.M);

            Button1Enum extraButtons1 = (Button1Enum)Data[EXTRABUTTON1_IDX];
            Inputs.ButtonState[ButtonFlags.L4] = extraButtons1.HasFlag(Button1Enum.M1);
            Inputs.ButtonState[ButtonFlags.R4] = extraButtons1.HasFlag(Button1Enum.M2);

            Button2Enum extraButtons2 = (Button2Enum)Data[EXTRABUTTON2_IDX];
            Inputs.ButtonState[ButtonFlags.B5] = extraButtons1.HasFlag(Button1Enum.C1);
            Inputs.ButtonState[ButtonFlags.B6] = extraButtons1.HasFlag(Button1Enum.C2);
            Inputs.ButtonState[ButtonFlags.B7] = extraButtons1.HasFlag(Button1Enum.C3);
            Inputs.ButtonState[ButtonFlags.B8] = extraButtons2.HasFlag(Button2Enum.C4);

            Inputs.ButtonState[ButtonFlags.B9] = extraButtons1.HasFlag(Button1Enum.T1);
            Inputs.ButtonState[ButtonFlags.B10] = extraButtons1.HasFlag(Button1Enum.T2);
            Inputs.ButtonState[ButtonFlags.B11] = extraButtons1.HasFlag(Button1Enum.T3);

            aX = (short)(Data[14] << 8 | Data[15]) * (4.0f / short.MaxValue);
            aZ = (short)(Data[16] << 8 | Data[17]) * -(4.0f / short.MaxValue);
            aY = (short)(Data[18] << 8 | Data[19]) * (4.0f / short.MaxValue);

            gX = (short)(Data[20] << 8 | Data[21]) * (2000.0f / short.MaxValue);
            gZ = (short)(Data[22] << 8 | Data[23]) * -(2000.0f / short.MaxValue);
            gY = (short)(Data[24] << 8 | Data[25]) * (2000.0f / short.MaxValue);

            // compute motion from controller
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);

            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            base.UpdateInputs(ticks, delta);
        }

        private void dataThreadLoop(object? obj)
        {
            // pull latest Data
            while (dataThreadRunning)
            {
                HidDeviceData report = hidDevice?.ReadData(0);
                if (report is not null)
                    Buffer.BlockCopy(report.Data, 1, Data, 0, report.Data.Length - 1);
            }
        }

        public override string GetGlyph(ButtonFlags button)
        {
            return base.GetGlyph(button);
        }
    }
}