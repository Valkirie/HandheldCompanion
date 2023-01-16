using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;

namespace HandheldCompanion.Controllers
{
    public class DS4Controller : DInputController
    {
        public DS4Controller(Joystick joystick, PnPDetails details) : base(joystick, details)
        {
            if (!IsConnected())
                return;

            InputsTimer.Tick += (sender, e) => UpdateInputs();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return joystick.Information.ProductName;
        }

        public override void UpdateInputs()
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            try
            {
                // Poll events from joystick
                joystick.Poll();

                // update gamepad state
                State = joystick.GetCurrentState();
            }
            catch { }

            if (prevState.GetHashCode() == State.GetHashCode() && prevInjectedButtons.Equals(InjectedButtons))
                return;

            Inputs.ButtonState = InjectedButtons as ButtonState;

            Inputs.ButtonState[ButtonFlags.B1] = State.Buttons[1];
            Inputs.ButtonState[ButtonFlags.B2] = State.Buttons[2];
            Inputs.ButtonState[ButtonFlags.B3] = State.Buttons[0];
            Inputs.ButtonState[ButtonFlags.B4] = State.Buttons[3];

            Inputs.ButtonState[ButtonFlags.Back] = State.Buttons[8];
            Inputs.ButtonState[ButtonFlags.Start] = State.Buttons[9];

            Inputs.ButtonState[ButtonFlags.L2] = State.Buttons[6];
            Inputs.ButtonState[ButtonFlags.R2] = State.Buttons[7];

            Inputs.ButtonState[ButtonFlags.LeftThumb] = State.Buttons[10];
            Inputs.ButtonState[ButtonFlags.RightThumb] = State.Buttons[11];

            Inputs.ButtonState[ButtonFlags.L1] = State.Buttons[4];
            Inputs.ButtonState[ButtonFlags.R1] = State.Buttons[5];

            Inputs.ButtonState[ButtonFlags.Special] = State.Buttons[12];

            Inputs.ButtonState[ButtonFlags.LPadClick] = State.Buttons[13];
            Inputs.ButtonState[ButtonFlags.RPadClick] = State.Buttons[13];

            switch (State.PointOfViewControllers[0])
            {
                case 0:
                    Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                    break;
                case 4500:
                    Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                    Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                    break;
                case 9000:
                    Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                    break;
                case 13500:
                    Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                    Inputs.ButtonState[ButtonFlags.DPadRight] = true;
                    break;
                case 18000:
                    Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                    break;
                case 22500:
                    Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                    Inputs.ButtonState[ButtonFlags.DPadDown] = true;
                    break;
                case 27000:
                    Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                    break;
                case 31500:
                    Inputs.ButtonState[ButtonFlags.DPadLeft] = true;
                    Inputs.ButtonState[ButtonFlags.DPadUp] = true;
                    break;
            }

            Inputs.AxisState[AxisFlags.R2] = (short)(State.RotationY * byte.MaxValue / ushort.MaxValue);
            Inputs.AxisState[AxisFlags.L2] = (short)(State.RotationX * byte.MaxValue / ushort.MaxValue);

            Inputs.AxisState[AxisFlags.LeftThumbX] = (short)(Math.Clamp(State.X - short.MaxValue, short.MinValue, short.MaxValue));
            Inputs.AxisState[AxisFlags.LeftThumbY] = (short)(Math.Clamp(-State.Y + short.MaxValue, short.MinValue, short.MaxValue));

            Inputs.AxisState[AxisFlags.RightThumbX] = (short)(Math.Clamp(State.Z - short.MaxValue, short.MinValue, short.MaxValue));
            Inputs.AxisState[AxisFlags.RightThumbY] = (short)(Math.Clamp(-State.RotationZ + short.MaxValue, short.MinValue, short.MaxValue));

            base.UpdateInputs();
        }

        public override bool IsConnected()
        {
            return (bool)(!joystick?.IsDisposed);
        }

        public override void Plug()
        {
            base.Plug();
        }

        public override void Unplug()
        {
            base.Unplug();
        }
    }
}
