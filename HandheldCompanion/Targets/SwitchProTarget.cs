using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System.Numerics;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    internal class SwitchProTarget : VIIPERTarget
    {
        // NS2 Pro wire format button layout.
        private const uint ButtonB = 1u << 0;
        private const uint ButtonA = 1u << 1;
        private const uint ButtonY = 1u << 2;
        private const uint ButtonX = 1u << 3;
        private const uint ButtonR = 1u << 4;
        private const uint ButtonZR = 1u << 5;
        private const uint ButtonPlus = 1u << 6;
        private const uint ButtonRStick = 1u << 7;
        private const uint ButtonDDown = 1u << 8;
        private const uint ButtonDRight = 1u << 9;
        private const uint ButtonDLeft = 1u << 10;
        private const uint ButtonDUp = 1u << 11;
        private const uint ButtonL = 1u << 12;
        private const uint ButtonZL = 1u << 13;
        private const uint ButtonMinus = 1u << 14;
        private const uint ButtonLStick = 1u << 15;
        private const uint ButtonHome = 1u << 16;
        private const uint ButtonCapture = 1u << 17;
        private const uint ButtonGR = 1u << 18;
        private const uint ButtonGL = 1u << 19;
        private const uint ButtonC = 1u << 20;

        private const ushort StickMin = 0x0000;
        private const ushort StickCenter = 0x0800;
        private const ushort StickMax = 0x0FFF;

        // IMU scaling — values placed into InputState are raw sensor units:
        //   Accel: SDL calibration expects 4096 LSB/g  (coeff=0x4000, formula: raw/4096 = g)
        //   Gyro:  SDL calibration expects 16.384 LSB/dps (coeff=0x3BF7, formula: raw*0.061 = dps)
        private const float AccelCountsPerG = 4096.0f;
        private const float GyroUnitsPerDps = 16.384f;

        // The Go VIIPER layer duplicates the same InputState gyro/accel values across all 3 IMU
        // frames in the 0x30 HID report. SDL fires one sensor event per frame, so the consumer
        // sees 3 identical events instead of 3 distinct ones — tripling the effective rate.
        // Divide gyro by 3 so the integrated sum over 3 frames equals the intended single-frame value.
        private const float GyroFrameDivider = 3.0f;

        private const int InputStateSize = 24; // ns2pro InputWireSize
        protected override int InputLength => InputStateSize;

        protected override string DeviceType => "ns2pro";
        public override int? MasterIntervalOverrideHz => 125;

        public SwitchProTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.SwitchProController;
            _reportBuffer = new byte[InputLength];
            LogManager.LogInformation("{0} initialized for VIIPER ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            uint buttons = 0;

            if (inputs.ButtonState[ButtonFlags.B1]) buttons |= ButtonB;
            if (inputs.ButtonState[ButtonFlags.B2]) buttons |= ButtonA;
            if (inputs.ButtonState[ButtonFlags.B3]) buttons |= ButtonY;
            if (inputs.ButtonState[ButtonFlags.B4]) buttons |= ButtonX;
            if (inputs.ButtonState[ButtonFlags.R1]) buttons |= ButtonR;
            if (inputs.AxisState[AxisFlags.R2] > 0) buttons |= ButtonZR;
            if (inputs.ButtonState[ButtonFlags.Start]) buttons |= ButtonPlus;
            if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= ButtonRStick;
            if (inputs.ButtonState[ButtonFlags.DPadDown]) buttons |= ButtonDDown;
            if (inputs.ButtonState[ButtonFlags.DPadRight]) buttons |= ButtonDRight;
            if (inputs.ButtonState[ButtonFlags.DPadLeft]) buttons |= ButtonDLeft;
            if (inputs.ButtonState[ButtonFlags.DPadUp]) buttons |= ButtonDUp;
            if (inputs.ButtonState[ButtonFlags.L1]) buttons |= ButtonL;
            if (inputs.AxisState[AxisFlags.L2] > 0) buttons |= ButtonZL;
            if (inputs.ButtonState[ButtonFlags.Back]) buttons |= ButtonMinus;
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= ButtonLStick;
            if (inputs.ButtonState[ButtonFlags.B5]) buttons |= ButtonHome;
            if (inputs.ButtonState[ButtonFlags.B6]) buttons |= ButtonCapture;

            ushort lx = MapStickAxis((short)inputs.AxisState[AxisFlags.LeftStickX]);
            ushort ly = MapStickAxis((short)inputs.AxisState[AxisFlags.LeftStickY]);
            ushort rx = MapStickAxis((short)inputs.AxisState[AxisFlags.RightStickX]);
            ushort ry = MapStickAxis((short)inputs.AxisState[AxisFlags.RightStickY]);

            ushort gyroX = 0, gyroY = 0, gyroZ = 0;
            ushort accelX = 0, accelY = 0, accelZ = 0;

            Vector3 gyro;
            Vector3 accel;
            if (gamepadMotion is not null)
            {
                gamepadMotion.GetRawGyro(out gyro.X, out gyro.Y, out gyro.Z);
                gamepadMotion.GetRawAcceleration(out accel.X, out accel.Y, out accel.Z);
            }
            else
            {
                gyro = inputs.GyroState.GetGyroscope(GyroState.SensorState.DSU);
                accel = inputs.GyroState.GetAccelerometer(GyroState.SensorState.DSU);
            }

            gyroX = (ushort)(-gyro.X * GyroUnitsPerDps / GyroFrameDivider);
            gyroY = (ushort)(-gyro.Z * GyroUnitsPerDps / GyroFrameDivider);
            gyroZ = (ushort)(gyro.Y * GyroUnitsPerDps / GyroFrameDivider);
            accelX = (ushort)(-accel.X * AccelCountsPerG / GyroFrameDivider);
            accelY = (ushort)(-accel.Z * AccelCountsPerG / GyroFrameDivider);
            accelZ = (ushort)(accel.Y * AccelCountsPerG / GyroFrameDivider);

            byte[] data = _reportBuffer;
            WriteU32(data, 0, buttons);
            WriteU16(data, 4, lx);
            WriteU16(data, 6, ly);
            WriteU16(data, 8, rx);
            WriteU16(data, 10, ry);
            WriteU16(data, 12, accelX);
            WriteU16(data, 14, accelY);
            WriteU16(data, 16, accelZ);
            WriteU16(data, 18, gyroX);
            WriteU16(data, 20, gyroY);
            WriteU16(data, 22, gyroZ);
            return data;
        }

        private static ushort MapStickAxis(short value)
        {
            return InputUtils.ClampToUShort(InputUtils.RoundToInt(InputUtils.MapRange(value, short.MinValue, short.MaxValue, StickMin, StickMax)), StickMin, StickMax);
        }

        // The Go layer handles all subcommand/USB/HID protocol internally.
        // Feedback arrives as a 2-byte OutputState: [0]=RumbleLeft, [1]=RumbleRight.
        protected override void HandleOutput(byte[] buffer)
        {
            if (buffer is null || buffer.Length < 2)
                return;

            SendVibrate(buffer[0], buffer[1]);
        }

        private static void WriteU32(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteU16(byte[] buf, int offset, ushort value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteI16(byte[] buf, int offset, short value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
