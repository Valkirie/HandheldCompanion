using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    internal class DualShock4Target : VIIPERTarget
    {
        protected override string DeviceType => "dualshock4";
        protected override int InputLength => 31;

        public DualShock4Target(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.DualShock4Controller;
            _reportBuffer = new byte[InputLength];

            LogManager.LogInformation("{0} initialized for VIIPER ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            byte[] data = _reportBuffer;
            Array.Clear(data, 0, data.Length);
            data[0] = (byte)(inputs.AxisState[AxisFlags.LeftStickX] >> 8);
            data[1] = (byte)(InputUtils.NegateClampToShort((short)inputs.AxisState[AxisFlags.LeftStickY]) >> 8);
            data[2] = (byte)(inputs.AxisState[AxisFlags.RightStickX] >> 8);
            data[3] = (byte)(InputUtils.NegateClampToShort((short)inputs.AxisState[AxisFlags.RightStickY]) >> 8);

            ushort buttons = 0;
            if (inputs.ButtonState[ButtonFlags.B1]) buttons |= 0x0020;
            if (inputs.ButtonState[ButtonFlags.B2]) buttons |= 0x0040;
            if (inputs.ButtonState[ButtonFlags.B3]) buttons |= 0x0010;
            if (inputs.ButtonState[ButtonFlags.B4]) buttons |= 0x0080;
            if (inputs.ButtonState[ButtonFlags.L1]) buttons |= 0x0100;
            if (inputs.ButtonState[ButtonFlags.R1]) buttons |= 0x0200;
            if (inputs.AxisState[AxisFlags.L2] > 0) buttons |= 0x0400;
            if (inputs.AxisState[AxisFlags.R2] > 0) buttons |= 0x0800;
            if (inputs.ButtonState[ButtonFlags.Back]) buttons |= 0x1000;
            if (inputs.ButtonState[ButtonFlags.Start]) buttons |= 0x2000;
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= 0x4000;
            if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= 0x8000;
            if (inputs.ButtonState[ButtonFlags.Special]) buttons |= 0x0001;
            if (inputs.ButtonState[ButtonFlags.LeftPadClick] || inputs.ButtonState[ButtonFlags.RightPadClick] || inputs.ButtonState[ButtonFlags.TouchpadClick] || DS4Touch.OutputClickButton) buttons |= 0x0002;
            data[4] = (byte)(buttons & 0xFF);
            data[5] = (byte)((buttons >> 8) & 0xFF);

            byte dpad = 0;
            if (inputs.ButtonState[ButtonFlags.DPadUp]) dpad |= 0x01;
            if (inputs.ButtonState[ButtonFlags.DPadDown]) dpad |= 0x02;
            if (inputs.ButtonState[ButtonFlags.DPadLeft]) dpad |= 0x04;
            if (inputs.ButtonState[ButtonFlags.DPadRight]) dpad |= 0x08;
            data[6] = dpad;
            data[7] = (byte)inputs.AxisState[AxisFlags.L2];
            data[8] = (byte)inputs.AxisState[AxisFlags.R2];

            var leftPadTouch = DS4Touch.OutputLeftPadTouch.IsActive
                ? DS4Touch.OutputLeftPadTouch
                : DS4Touch.LeftPadTouch;
            if (leftPadTouch.IsActive)
            {
                ushort x = InputUtils.ClampToUShort((int)leftPadTouch.X, 0, DS4Touch.TOUCHPAD_WIDTH - 1);
                ushort y = InputUtils.ClampToUShort((int)leftPadTouch.Y, 0, DS4Touch.TOUCHPAD_HEIGHT - 1);
                data[9] = (byte)(x & 0xFF);
                data[10] = (byte)((x >> 8) & 0xFF);
                data[11] = (byte)(y & 0xFF);
                data[12] = (byte)((y >> 8) & 0xFF);
                data[13] = 1;
            }

            var rightPadTouch = DS4Touch.OutputRightPadTouch.IsActive
                ? DS4Touch.OutputRightPadTouch
                : DS4Touch.RightPadTouch;
            if (rightPadTouch.IsActive)
            {
                ushort x = InputUtils.ClampToUShort((int)rightPadTouch.X, 0, DS4Touch.TOUCHPAD_WIDTH - 1);
                ushort y = InputUtils.ClampToUShort((int)rightPadTouch.Y, 0, DS4Touch.TOUCHPAD_HEIGHT - 1);
                data[14] = (byte)(x & 0xFF);
                data[15] = (byte)((x >> 8) & 0xFF);
                data[16] = (byte)(y & 0xFF);
                data[17] = (byte)((y >> 8) & 0xFF);
                data[18] = 1;
            }

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
                data[19] = (byte)(gxs & 0xFF); data[20] = (byte)((gxs >> 8) & 0xFF);
                data[21] = (byte)(gys & 0xFF); data[22] = (byte)((gys >> 8) & 0xFF);
                data[23] = (byte)(gzs & 0xFF); data[24] = (byte)((gzs >> 8) & 0xFF);
                data[25] = (byte)(axs & 0xFF); data[26] = (byte)((axs >> 8) & 0xFF);
                data[27] = (byte)(ays & 0xFF); data[28] = (byte)((ays >> 8) & 0xFF);
                data[29] = (byte)(azs & 0xFF); data[30] = (byte)((azs >> 8) & 0xFF);
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
                data[19] = (byte)(gxs & 0xFF); data[20] = (byte)((gxs >> 8) & 0xFF);
                data[21] = (byte)(gys & 0xFF); data[22] = (byte)((gys >> 8) & 0xFF);
                data[23] = (byte)(gzs & 0xFF); data[24] = (byte)((gzs >> 8) & 0xFF);
                data[25] = (byte)(axs & 0xFF); data[26] = (byte)((axs >> 8) & 0xFF);
                data[27] = (byte)(ays & 0xFF); data[28] = (byte)((ays >> 8) & 0xFF);
                data[29] = (byte)(azs & 0xFF); data[30] = (byte)((azs >> 8) & 0xFF);
            }

            return data;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}