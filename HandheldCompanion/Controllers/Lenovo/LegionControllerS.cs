using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Controllers.Lenovo
{
    public class LegionControllerS : XInputController
    {
        private controller_hidapi.net.LegionController Controller;
        private byte[] data = new byte[64];

        public override bool IsReady => true;
        public override string ToString() => "Legion Controller";

        public LegionControllerS() : base()
        {
            Capabilities |= ControllerCapabilities.MotionSensor;
        }

        public LegionControllerS(PnPDetails details) : base(details)
        { }

        protected override void InitializeInputOutput()
        {
            // Additional controller specific source buttons
            SourceButtons.Add(ButtonFlags.RightPadClick);
            SourceButtons.Add(ButtonFlags.RightPadTouch);

            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.L4);

            // Legion Controllers do not have the Special button
            SourceButtons.Remove(ButtonFlags.Special);

            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = Controller?.Reading == true && Controller?.IsDeviceValid == true;
            if (WasPlugged) Close();

            // create controller
            // todo: improve detection (usagePage / usage)
            Controller = new(details.VendorID, details.ProductID, 33);

            // open controller as we need to check if it's ready by polling the hiddevice
            Open();
        }

        private void Open()
        {
            lock (hidLock)
            {
                try
                {
                    if (Controller is not null)
                    {
                        // open controller
                        Controller.OnControllerInputReceived += Controller_OnControllerInputReceived;
                        Controller.Open();
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(LegionController), ex.Message);
                    return;
                }
            }
        }

        private void Close()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    // close controller
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.Close();
                }
            }
        }

        public override void Gone()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.EndRead();
                    Controller = null;
                }
            }
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            Buffer.BlockCopy(Data, 1, this.data, 0, Data.Length - 1);
        }

        public override void Plug()
        {
            Open();

            base.Plug();
        }

        public override void Unplug()
        {
            Close();

            base.Unplug();
        }

        public override void UpdateInputs(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            base.UpdateInputs(ticks, delta, false);

            // Front buttons
            byte byte0 = data[0]; // Front buttons
            Inputs.ButtonState[ButtonFlags.OEM1] = (byte0 & (1 << 7)) != 0; // LegionR
            Inputs.ButtonState[ButtonFlags.OEM2] = (byte0 & (1 << 6)) != 0; // LegionL

            // --- Extra Button Parsing ---
            byte byte2 = data[2]; // (2 << 3) = byte 23
            Inputs.ButtonState[ButtonFlags.L4] = (byte2 & (1 << 7)) != 0;  // extra_l1 → B5 → bit 7
            Inputs.ButtonState[ButtonFlags.R4] = (byte2 & (1 << 6)) != 0;  // extra_r1 → B6 → bit 6

            // Example parsing assuming positions from const.py
            aX = BitConverter.ToInt16(data, 14) * -0.00212f;
            aZ = BitConverter.ToInt16(data, 16) * -0.00212f;
            aY = BitConverter.ToInt16(data, 18) * -0.00212f;

            gX = BitConverter.ToInt16(data, 20) * -0.0005325f;
            gZ = BitConverter.ToInt16(data, 22) * 0.0005325f;
            gY = BitConverter.ToInt16(data, 24) * -0.0005325f;

            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            // Touchpad parsing (2 bytes each, centered, absolute)
            ushort tpX = BitConverter.ToUInt16(data, 2);
            ushort tpY = BitConverter.ToUInt16(data, 4);

            bool touched = (tpX != 0 || tpY != 0);
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = touched;

            if (touched)
            {
                Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange((short)tpX, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange((short)-tpY, 0, 1000, short.MinValue, short.MaxValue);
            }
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
            }

            base.UpdateInputs(ticks, delta);
        }
    }
}
