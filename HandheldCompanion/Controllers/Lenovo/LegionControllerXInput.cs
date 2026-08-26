using HandheldCompanion.Inputs;
using SharpDX.XInput;

namespace HandheldCompanion.Controllers.Lenovo
{
    public class LegionControllerXInput : LegionController, IXInputController
    {
        private Controller? xinputController;
        private Gamepad gamepad;

        public LegionControllerXInput() : base()
        { }

        public LegionControllerXInput(PnPDetails details) : base(details)
        {
            Capabilities |= ControllerCapabilities.Rumble;
        }

        public override void AttachDetails(PnPDetails details)
        {
            AttachController(details.XInputUserIndex);
            base.AttachDetails(details);
        }

        public override bool IsConnected() => xinputController?.IsConnected == true;

        public override void SetVibration(byte largeMotor, byte smallMotor)
        {
            if (!IsConnected())
                return;

            try
            {
                xinputController?.SetVibration(new Vibration
                {
                    LeftMotorSpeed = (ushort)((double)largeMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength),
                    RightMotorSpeed = (ushort)((double)smallMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength)
                });
            }
            catch { }
        }

        public void AttachController(byte userIndex)
        {
            if (userIndex == byte.MaxValue)
                return;

            if (UserIndex == userIndex && xinputController?.IsConnected == true)
                return;

            UserIndex = userIndex;
            xinputController = new Controller((UserIndex)userIndex);
        }

        protected override bool UpdateState()
        {
            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

            if (!IsConnected())
                return false;

            try
            {
                if (xinputController is null)
                    return false;

                gamepad = xinputController.GetState().Gamepad;

                Inputs.ButtonState[ButtonFlags.B1] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
                Inputs.ButtonState[ButtonFlags.B2] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
                Inputs.ButtonState[ButtonFlags.B3] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
                Inputs.ButtonState[ButtonFlags.B4] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
                Inputs.ButtonState[ButtonFlags.Start] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
                Inputs.ButtonState[ButtonFlags.Back] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);
                Inputs.ButtonState[ButtonFlags.L2Soft] |= gamepad.LeftTrigger > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.R2Soft] |= gamepad.RightTrigger > TriggerThreshold;
                Inputs.ButtonState[ButtonFlags.L2Full] |= gamepad.LeftTrigger > TriggerThreshold * 8;
                Inputs.ButtonState[ButtonFlags.R2Full] |= gamepad.RightTrigger > TriggerThreshold * 8;
                Inputs.ButtonState[ButtonFlags.LeftStickClick] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
                Inputs.ButtonState[ButtonFlags.RightStickClick] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
                Inputs.ButtonState[ButtonFlags.L1] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
                Inputs.ButtonState[ButtonFlags.R1] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
                Inputs.ButtonState[ButtonFlags.DPadUp] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
                Inputs.ButtonState[ButtonFlags.DPadDown] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
                Inputs.ButtonState[ButtonFlags.DPadLeft] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
                Inputs.ButtonState[ButtonFlags.DPadRight] |= gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);

                Inputs.AxisState[AxisFlags.LeftStickX] = gamepad.LeftThumbX;
                Inputs.AxisState[AxisFlags.LeftStickY] = gamepad.LeftThumbY;
                Inputs.AxisState[AxisFlags.RightStickX] = gamepad.RightThumbX;
                Inputs.AxisState[AxisFlags.RightStickY] = gamepad.RightThumbY;
                Inputs.AxisState[AxisFlags.L2] = gamepad.LeftTrigger;
                Inputs.AxisState[AxisFlags.R2] = gamepad.RightTrigger;

                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SetVibration(0, 0);
                xinputController = null;
            }

            base.Dispose(disposing);
        }
    }
}
