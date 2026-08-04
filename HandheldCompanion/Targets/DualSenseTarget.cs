using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    internal class DualSenseTarget : VIIPERTarget
    {
        protected override string DeviceType => "dualsenseedge";
        protected override int InputLength => 33;

        private enum ClickRegion : byte { None, Left, Right, Coordinate }
        private ClickRegion preparedClickRegion;
        private bool preparedClickEmitted;
        private int preparedTouchX;
        private int preparedTouchY;

        public DualSenseTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.DualSenseController;
            _reportBuffer = new byte[InputLength];

            LogManager.LogInformation("{0} initialized for VIIPER ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override void HandleOutput(byte[] buffer)
        {
            if (buffer.Length >= 2)
                SendVibrate(buffer[1], buffer[0]);
        }

        protected override byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            byte[] data = _reportBuffer;
            Array.Clear(data, 0, data.Length);
            data[0] = (byte)(inputs.AxisState[AxisFlags.LeftStickX] >> 8);
            data[1] = (byte)(InputUtils.NegateClampToShort((short)inputs.AxisState[AxisFlags.LeftStickY]) >> 8);
            data[2] = (byte)(inputs.AxisState[AxisFlags.RightStickX] >> 8);
            data[3] = (byte)(InputUtils.NegateClampToShort((short)inputs.AxisState[AxisFlags.RightStickY]) >> 8);

            bool leftPadClick = inputs.ButtonState[ButtonFlags.LeftPadClick];
            bool rightPadClick = inputs.ButtonState[ButtonFlags.RightPadClick];
            bool coordinateClick = inputs.ButtonState[ButtonFlags.TouchpadCoordinateClick];
            bool centerPadClick = !coordinateClick && !leftPadClick && !rightPadClick &&
                (inputs.ButtonState[ButtonFlags.CenterPadClick] ||
                (DS4Touch.OutputClickButton && !leftPadClick && !rightPadClick));

            int coordinateX = DS4Touch.AxisToCoordinate(inputs.AxisState[AxisFlags.RightPadX], DS4Touch.TOUCHPAD_WIDTH);
            int coordinateY = DS4Touch.TOUCHPAD_HEIGHT - 1 -
                DS4Touch.AxisToCoordinate(inputs.AxisState[AxisFlags.RightPadY], DS4Touch.TOUCHPAD_HEIGHT);

            ClickRegion requestedRegion = coordinateClick ? ClickRegion.Coordinate :
                leftPadClick ? ClickRegion.Left :
                rightPadClick ? ClickRegion.Right : ClickRegion.None;

            // Steam must see the regional touch before the shared click transitions
            // down. Otherwise it first classifies the press as the center button.
            bool emitPadClick = centerPadClick;
            ClickRegion touchClickRegion = requestedRegion;
            bool consumePreparedClick = false;
            if (requestedRegion != ClickRegion.None)
            {
                if (requestedRegion == ClickRegion.Coordinate)
                {
                    preparedTouchX = coordinateX;
                    preparedTouchY = coordinateY;
                }

                if (preparedClickRegion == requestedRegion)
                {
                    emitPadClick = true;
                    preparedClickEmitted = true;
                }
                else
                {
                    preparedClickRegion = requestedRegion;
                    preparedClickEmitted = false;
                }
            }
            else if (preparedClickRegion != ClickRegion.None && !preparedClickEmitted)
            {
                // Preserve a one-report tap long enough to emit the click after
                // its preparatory touch report.
                emitPadClick = true;
                touchClickRegion = preparedClickRegion;
                consumePreparedClick = true;
            }
            else
            {
                ClearPreparedClick();
            }

            uint buttons = 0;
            if (inputs.ButtonState[ButtonFlags.B1]) buttons |= 0x0020;
            if (inputs.ButtonState[ButtonFlags.B2]) buttons |= 0x0040;
            if (inputs.ButtonState[ButtonFlags.B3]) buttons |= 0x0010;
            if (inputs.ButtonState[ButtonFlags.B4]) buttons |= 0x0080;
            if (inputs.ButtonState[ButtonFlags.L1]) buttons |= 0x0100;
            if (inputs.ButtonState[ButtonFlags.R1]) buttons |= 0x0200;
            if (inputs.ButtonState[ButtonFlags.Back]) buttons |= 0x1000;
            if (inputs.ButtonState[ButtonFlags.Start]) buttons |= 0x2000;
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= 0x4000;
            if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= 0x8000;
            if (inputs.ButtonState[ButtonFlags.Special]) buttons |= 0x00010000;
            if (emitPadClick) buttons |= 0x00020000;
            if (inputs.ButtonState[ButtonFlags.MicrophoneMute]) buttons |= 0x00040000;
            data[4] = (byte)(buttons & 0xFF);
            data[5] = (byte)((buttons >> 8) & 0xFF);
            data[6] = (byte)((buttons >> 16) & 0xFF);
            data[7] = (byte)((buttons >> 24) & 0xFF);

            byte dpad = 0;
            if (inputs.ButtonState[ButtonFlags.DPadUp]) dpad |= 0x01;
            if (inputs.ButtonState[ButtonFlags.DPadDown]) dpad |= 0x02;
            if (inputs.ButtonState[ButtonFlags.DPadLeft]) dpad |= 0x04;
            if (inputs.ButtonState[ButtonFlags.DPadRight]) dpad |= 0x08;
            data[8] = dpad;
            data[9] = (byte)inputs.AxisState[AxisFlags.L2];
            data[10] = (byte)inputs.AxisState[AxisFlags.R2];

            // A DualSense only has one physical touchpad-click button. Steam splits
            // left and right from center using a touch which is already active when
            // that button goes down. A center click is sent without an active touch.
            bool coordinateTouch = coordinateClick || inputs.ButtonState[ButtonFlags.TouchpadCoordinateTouch] ||
                inputs.ButtonState[ButtonFlags.TouchpadSwipe];

            data[15] = 0; // VIIPER Touch1Active validity flag
            if (touchClickRegion == ClickRegion.Coordinate)
                WriteTouch(data, coordinateClick ? coordinateX : preparedTouchX,
                    coordinateClick ? coordinateY : preparedTouchY);
            else if (touchClickRegion == ClickRegion.Left)
                WriteTouch(data, DS4Touch.TOUCHPAD_WIDTH / 4, DS4Touch.TOUCHPAD_HEIGHT / 2);
            else if (touchClickRegion == ClickRegion.Right)
                WriteTouch(data, DS4Touch.TOUCHPAD_WIDTH * 3 / 4, DS4Touch.TOUCHPAD_HEIGHT / 2);
            else if (coordinateTouch)
                WriteTouch(data, coordinateX, coordinateY);
            else if (!centerPadClick && DS4Touch.LeftPadTouch.IsActive)
                WriteTouch(data, DS4Touch.LeftPadTouch.X, DS4Touch.LeftPadTouch.Y);
            else if (!centerPadClick && DS4Touch.RightPadTouch.IsActive)
                WriteTouch(data, DS4Touch.RightPadTouch.X, DS4Touch.RightPadTouch.Y);

            if (consumePreparedClick)
                ClearPreparedClick();

            if (gamepadMotion is not null)
            {
                gamepadMotion.GetRawGyro(out float gx, out float gy, out float gz);
                gamepadMotion.GetRawAcceleration(out float ax, out float ay, out float az);
                short gxs = InputUtils.RoundClampToShort(InputUtils.Clamp(gx, -2048.0f, 2048.0f) * 16.0f);
                short gys = InputUtils.RoundClampToShort(InputUtils.Clamp(gy, -2048.0f, 2048.0f) * 16.0f);
                short gzs = InputUtils.RoundClampToShort(InputUtils.Clamp(gz, -2048.0f, 2048.0f) * 16.0f);
                short axs = InputUtils.RoundClampToShort(InputUtils.Clamp(ax * 9.81f, -64.0f, 64.0f) * 512.0f);
                short ays = InputUtils.RoundClampToShort(InputUtils.Clamp(ay * 9.81f, -64.0f, 64.0f) * 512.0f);
                short azs = InputUtils.RoundClampToShort(InputUtils.Clamp(az * 9.81f, -64.0f, 64.0f) * 512.0f);
                data[21] = (byte)(gxs & 0xFF); data[22] = (byte)((gxs >> 8) & 0xFF);
                data[23] = (byte)(gys & 0xFF); data[24] = (byte)((gys >> 8) & 0xFF);
                data[25] = (byte)(gzs & 0xFF); data[26] = (byte)((gzs >> 8) & 0xFF);
                data[27] = (byte)(axs & 0xFF); data[28] = (byte)((axs >> 8) & 0xFF);
                data[29] = (byte)(ays & 0xFF); data[30] = (byte)((ays >> 8) & 0xFF);
                data[31] = (byte)(azs & 0xFF); data[32] = (byte)((azs >> 8) & 0xFF);
            }
            else
            {
                var gyro = inputs.GyroState.GetGyroscope(GyroState.SensorState.DSU);
                var accel = inputs.GyroState.GetAccelerometer(GyroState.SensorState.DSU);
                short gxs = InputUtils.RoundClampToShort(InputUtils.Clamp(gyro.X, -2048.0f, 2048.0f) * 16.0f);
                short gys = InputUtils.RoundClampToShort(InputUtils.Clamp(gyro.Y, -2048.0f, 2048.0f) * 16.0f);
                short gzs = InputUtils.RoundClampToShort(InputUtils.Clamp(gyro.Z, -2048.0f, 2048.0f) * 16.0f);
                short axs = InputUtils.RoundClampToShort(InputUtils.Clamp(accel.X * 9.81f, -64.0f, 64.0f) * 512.0f);
                short ays = InputUtils.RoundClampToShort(InputUtils.Clamp(accel.Y * 9.81f, -64.0f, 64.0f) * 512.0f);
                short azs = InputUtils.RoundClampToShort(InputUtils.Clamp(accel.Z * 9.81f, -64.0f, 64.0f) * 512.0f);
                data[21] = (byte)(gxs & 0xFF); data[22] = (byte)((gxs >> 8) & 0xFF);
                data[23] = (byte)(gys & 0xFF); data[24] = (byte)((gys >> 8) & 0xFF);
                data[25] = (byte)(gzs & 0xFF); data[26] = (byte)((gzs >> 8) & 0xFF);
                data[27] = (byte)(axs & 0xFF); data[28] = (byte)((axs >> 8) & 0xFF);
                data[29] = (byte)(ays & 0xFF); data[30] = (byte)((ays >> 8) & 0xFF);
                data[31] = (byte)(azs & 0xFF); data[32] = (byte)((azs >> 8) & 0xFF);
            }

            return data;
        }

        private static void WriteTouch(byte[] data, int x, int y)
        {
            ushort touchX = InputUtils.ClampToUShort(x, 0, DS4Touch.TOUCHPAD_WIDTH - 1);
            ushort touchY = InputUtils.ClampToUShort(y, 0, DS4Touch.TOUCHPAD_HEIGHT - 1);
            data[11] = (byte)(touchX & 0xFF);
            data[12] = (byte)((touchX >> 8) & 0xFF);
            data[13] = (byte)(touchY & 0xFF);
            data[14] = (byte)((touchY >> 8) & 0xFF);
            data[15] = 1;
        }

        private void ClearPreparedClick()
        {
            preparedClickRegion = ClickRegion.None;
            preparedClickEmitted = false;
        }
    }
}
