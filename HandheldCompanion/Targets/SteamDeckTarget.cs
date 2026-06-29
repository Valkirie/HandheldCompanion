using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    internal class SteamDeckTarget : VIIPERTarget
    {
        private const byte FeatureTriggerHapticPulseCommand = 0x8F;
        private const byte FeatureTriggerHapticCommand = 0xEA;
        private const byte FeatureTriggerRumbleCommand = 0xEB;
        private const int InputReportId = 0x09;
        private const int InputPayloadLength = 56;
        private const int ImuOffset = 24;
        private const float GyroUnitsPerDps = 16.0f;
        private const float AccelCountsPerG = 16384.0f;

        protected override string DeviceType => "steamdeck";
        protected override int InputLength => 64;
        public override int? MasterIntervalOverrideHz => 250;

        public SteamDeckTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.SteamDeckController;
            _reportBuffer = new byte[InputLength];

            LogManager.LogInformation("{0} initialized for VIIPER ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            byte[] data = _reportBuffer;
            Array.Clear(data, 0, data.Length);
            data[0] = 0x01;
            data[1] = 0x00;
            data[2] = InputReportId;
            data[3] = (byte)InputLength;

            byte buttons8 = 0;
            if (inputs.ButtonState[ButtonFlags.B1]) buttons8 |= 0x80;
            if (inputs.ButtonState[ButtonFlags.B3]) buttons8 |= 0x40;
            if (inputs.ButtonState[ButtonFlags.B2]) buttons8 |= 0x20;
            if (inputs.ButtonState[ButtonFlags.B4]) buttons8 |= 0x10;
            if (inputs.ButtonState[ButtonFlags.L1]) buttons8 |= 0x08;
            if (inputs.ButtonState[ButtonFlags.R1]) buttons8 |= 0x04;
            if (inputs.AxisState[AxisFlags.L2] > 0) buttons8 |= 0x02;
            if (inputs.AxisState[AxisFlags.R2] > 0) buttons8 |= 0x01;
            data[8] = buttons8;

            byte buttons9 = 0;
            if (inputs.ButtonState[ButtonFlags.L5]) buttons9 |= 0x80;
            if (inputs.ButtonState[ButtonFlags.Back]) buttons9 |= 0x40;
            if (inputs.ButtonState[ButtonFlags.Special]) buttons9 |= 0x20;
            if (inputs.ButtonState[ButtonFlags.Start]) buttons9 |= 0x10;
            if (inputs.ButtonState[ButtonFlags.DPadDown]) buttons9 |= 0x08;
            if (inputs.ButtonState[ButtonFlags.DPadLeft]) buttons9 |= 0x04;
            if (inputs.ButtonState[ButtonFlags.DPadRight]) buttons9 |= 0x02;
            if (inputs.ButtonState[ButtonFlags.DPadUp]) buttons9 |= 0x01;
            data[9] = buttons9;

            byte buttons10 = 0;
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons10 |= 0x40;
            if (inputs.ButtonState[ButtonFlags.RightPadTouch]) buttons10 |= 0x10;
            if (inputs.ButtonState[ButtonFlags.LeftPadTouch]) buttons10 |= 0x08;
            if (inputs.ButtonState[ButtonFlags.RightPadClick]) buttons10 |= 0x04;
            if (inputs.ButtonState[ButtonFlags.LeftPadClick]) buttons10 |= 0x02;
            if (inputs.ButtonState[ButtonFlags.R5]) buttons10 |= 0x01;
            data[10] = buttons10;

            if (inputs.ButtonState[ButtonFlags.RightStickClick]) data[11] = 0x04;

            byte buttons13 = 0;
            if (inputs.ButtonState[ButtonFlags.RightStickTouch]) buttons13 |= 0x80;
            if (inputs.ButtonState[ButtonFlags.LeftStickTouch]) buttons13 |= 0x40;
            if (inputs.ButtonState[ButtonFlags.R4]) buttons13 |= 0x04;
            if (inputs.ButtonState[ButtonFlags.L4]) buttons13 |= 0x02;
            data[13] = buttons13;

            if (inputs.ButtonState[ButtonFlags.Special2]) data[14] = 0x04;

            WriteI16(data, 16, (short)inputs.AxisState[AxisFlags.LeftPadX]);
            WriteI16(data, 18, (short)inputs.AxisState[AxisFlags.LeftPadY]);
            WriteI16(data, 20, (short)inputs.AxisState[AxisFlags.RightPadX]);
            WriteI16(data, 22, (short)inputs.AxisState[AxisFlags.RightPadY]);

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

            short accelX = (short)(accel.X * AccelCountsPerG);
            short accelY = (short)(-accel.Z * AccelCountsPerG);
            short accelZ = (short)(accel.Y * AccelCountsPerG);
            short gyroPitch = (short)(gyro.X * GyroUnitsPerDps);
            short gyroYaw = (short)(-gyro.Z * GyroUnitsPerDps);
            short gyroRoll = (short)(gyro.Y * GyroUnitsPerDps);

            WriteI16(data, ImuOffset, accelX);
            WriteI16(data, ImuOffset + 2, accelY);
            WriteI16(data, ImuOffset + 4, accelZ);
            WriteI16(data, ImuOffset + 6, gyroPitch);
            WriteI16(data, ImuOffset + 8, gyroYaw);
            WriteI16(data, ImuOffset + 10, gyroRoll);

            if (gamepadMotion is not null)
            {
                Quaternion q = gamepadMotion.GetQuaternion();
                WriteI16(data, ImuOffset + 12, (short)(q.W * short.MaxValue));
                WriteI16(data, ImuOffset + 14, (short)(q.X * short.MaxValue));
                WriteI16(data, ImuOffset + 16, (short)(q.Y * short.MaxValue));
                WriteI16(data, ImuOffset + 18, (short)(q.Z * short.MaxValue));
            }

            WriteU16(data, 44, InputUtils.ClampToUShort(Math.Clamp((int)inputs.AxisState[AxisFlags.L2], 0, byte.MaxValue) * short.MaxValue / byte.MaxValue));
            WriteU16(data, 46, InputUtils.ClampToUShort(Math.Clamp((int)inputs.AxisState[AxisFlags.R2], 0, byte.MaxValue) * short.MaxValue / byte.MaxValue));
            WriteI16(data, 48, (short)inputs.AxisState[AxisFlags.LeftStickX]);
            WriteI16(data, 50, (short)inputs.AxisState[AxisFlags.LeftStickY]);
            WriteI16(data, 52, (short)inputs.AxisState[AxisFlags.RightStickX]);
            WriteI16(data, 54, (short)inputs.AxisState[AxisFlags.RightStickY]);
            WriteU16(data, 56, inputs.ButtonState[ButtonFlags.LeftPadClick] ? ushort.MaxValue : (ushort)0);
            WriteU16(data, 58, inputs.ButtonState[ButtonFlags.RightPadClick] ? ushort.MaxValue : (ushort)0);
            WriteU16(data, 60, inputs.ButtonState[ButtonFlags.LeftStickClick] ? ushort.MaxValue : (ushort)0);
            WriteU16(data, 62, inputs.ButtonState[ButtonFlags.RightStickClick] ? ushort.MaxValue : (ushort)0);

            return data;
        }

        protected override void HandleOutput(byte[] buffer)
        {
            if (buffer is null || buffer.Length == 0)
                return;

            switch (buffer[0])
            {
                case FeatureTriggerRumbleCommand when buffer.Length >= 9:
                    ushort leftSpeed = (ushort)(buffer[5] | (buffer[6] << 8));
                    ushort rightSpeed = (ushort)(buffer[7] | (buffer[8] << 8));
                    SendVibrate(ToMotorByte(leftSpeed), ToMotorByte(rightSpeed));
                    break;

                case FeatureTriggerHapticCommand when buffer.Length >= 6:
                    byte hapticStrength = ToHapticByte(buffer[4], unchecked((sbyte)buffer[5]));
                    SendVibrate(hapticStrength, hapticStrength);
                    break;

                case FeatureTriggerHapticPulseCommand when buffer.Length >= 10:
                    ushort pulsePeriod = (ushort)(buffer[5] | (buffer[6] << 8));
                    ushort pulseCount = (ushort)(buffer[7] | (buffer[8] << 8));
                    byte pulseStrength = ToPulseByte(pulseCount, buffer[9]);
                    SendVibrate(pulseStrength, pulseStrength);
                    int pulseDurationMs = (int)Math.Ceiling(pulsePeriod * (long)pulseCount / 1000.0);
                    _ = Task.Delay(Math.Max(pulseDurationMs, 1)).ContinueWith(_ => SendVibrate(0, 0));
                    break;
            }
        }

        private static byte ToMotorByte(ushort speed)
        {
            return InputUtils.ClampToByte(speed / 256);
        }

        private static byte ToHapticByte(byte intensity, sbyte gain)
        {
            int value = intensity + (gain * 8);
            return InputUtils.ClampToByte(Math.Max(value, 0));
        }

        private static byte ToPulseByte(ushort count, byte gain)
        {
            int value = Math.Min(255, (count * 16) + gain);
            return InputUtils.ClampToByte(value);
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
