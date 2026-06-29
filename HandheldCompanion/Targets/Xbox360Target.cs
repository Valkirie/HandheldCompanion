using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    internal partial class Xbox360Target : VIIPERTarget
    {
        protected override string DeviceType => "xbox360";
        protected override int InputLength => 20;

        public Xbox360Target(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.Xbox360Controller;
            _reportBuffer = new byte[InputLength];

            LogManager.LogInformation("{0} initialized for VIIPER ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            byte[] data = _reportBuffer;
            Array.Clear(data, 0, data.Length);
            uint buttons = 0;
            if (inputs.ButtonState[ButtonFlags.DPadUp]) buttons |= 0x0001;
            if (inputs.ButtonState[ButtonFlags.DPadDown]) buttons |= 0x0002;
            if (inputs.ButtonState[ButtonFlags.DPadLeft]) buttons |= 0x0004;
            if (inputs.ButtonState[ButtonFlags.DPadRight]) buttons |= 0x0008;
            if (inputs.ButtonState[ButtonFlags.Start]) buttons |= 0x0010;
            if (inputs.ButtonState[ButtonFlags.Back]) buttons |= 0x0020;
            if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= 0x0040;
            if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= 0x0080;
            if (inputs.ButtonState[ButtonFlags.L1]) buttons |= 0x0100;
            if (inputs.ButtonState[ButtonFlags.R1]) buttons |= 0x0200;
            if (inputs.ButtonState[ButtonFlags.Special]) buttons |= 0x0400;
            if (inputs.ButtonState[ButtonFlags.B1]) buttons |= 0x1000;
            if (inputs.ButtonState[ButtonFlags.B2]) buttons |= 0x2000;
            if (inputs.ButtonState[ButtonFlags.B3]) buttons |= 0x4000;
            if (inputs.ButtonState[ButtonFlags.B4]) buttons |= 0x8000;
            data[0] = (byte)(buttons & 0xFF);
            data[1] = (byte)((buttons >> 8) & 0xFF);
            data[2] = (byte)((buttons >> 16) & 0xFF);
            data[3] = (byte)((buttons >> 24) & 0xFF);
            data[4] = (byte)inputs.AxisState[AxisFlags.L2];
            data[5] = (byte)inputs.AxisState[AxisFlags.R2];
            short lx = (short)inputs.AxisState[AxisFlags.LeftStickX];
            short ly = (short)inputs.AxisState[AxisFlags.LeftStickY];
            short rx = (short)inputs.AxisState[AxisFlags.RightStickX];
            short ry = (short)inputs.AxisState[AxisFlags.RightStickY];
            data[6] = (byte)(lx & 0xFF); data[7] = (byte)((lx >> 8) & 0xFF);
            data[8] = (byte)(ly & 0xFF); data[9] = (byte)((ly >> 8) & 0xFF);
            data[10] = (byte)(rx & 0xFF); data[11] = (byte)((rx >> 8) & 0xFF);
            data[12] = (byte)(ry & 0xFF); data[13] = (byte)((ry >> 8) & 0xFF);
            return data;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}