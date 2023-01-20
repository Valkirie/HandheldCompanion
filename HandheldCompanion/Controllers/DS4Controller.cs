using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ModernWpf.Controls;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace HandheldCompanion.Controllers
{
    public class DS4Controller : DInputController
    {
        private static Dictionary<ButtonFlags, SolidColorBrush> ButtonColors = new()
        {
            { ButtonFlags.B1, new SolidColorBrush(Color.FromArgb(255, 116, 139, 255)) },
            { ButtonFlags.B2, new SolidColorBrush(Color.FromArgb(255, 255, 73, 75)) },
            { ButtonFlags.B3, new SolidColorBrush(Color.FromArgb(255, 244, 149, 193)) },
            { ButtonFlags.B4, new SolidColorBrush(Color.FromArgb(255, 73, 191, 115)) },
        };

        public DS4Controller(Joystick joystick, PnPDetails details) : base(joystick, details)
        {
            this.ControllerType = ControllerType.DS4;

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

            /*
            if (prevState.Buttons.Equals(State.Buttons) && prevState.PointOfViewControllers.Equals(State.PointOfViewControllers) && prevInjectedButtons.Equals(InjectedButtons))
                return;
            */

            Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

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

        public static new string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B1:
                    return "\u21E3"; // Cross
                case ButtonFlags.B2:
                    return "\u21E2"; // Circle
                case ButtonFlags.B3:
                    return "\u21E0"; // Square
                case ButtonFlags.B4:
                    return "\u21E1"; // Triangle
                case ButtonFlags.L1:
                    return "\u21B0";
                case ButtonFlags.R1:
                    return "\u21B1";
                case ButtonFlags.Back:
                    return "\u21E6";
                case ButtonFlags.Start:
                    return "\u21E8";
                case ButtonFlags.L2:
                    return "\u21B2";
                case ButtonFlags.R2:
                    return "\u21B3";
                case ButtonFlags.Special:
                    return "\uE000";
                case ButtonFlags.LPadClick:
                case ButtonFlags.RPadClick:
                    return "\u21E7";
            }

            return IController.GetGlyph(button);
        }

        public static new string GetGlyph(AxisFlags axis)
        {
            switch (axis)
            {
                case AxisFlags.L2:
                    return "\u21B2";
                case AxisFlags.R2:
                    return "\u21B3";
            }

            return IController.GetGlyph(axis);
        }

        public static FontIcon GetFontIcon(ButtonFlags button, int FontIconSize = 20)
        {
            var fontIcon = new FontIcon()
            {
                Glyph = GetGlyph(button),
                FontSize = FontIconSize,
                FontFamily = FontFamily
            };

            if (ButtonColors.ContainsKey(button))
                fontIcon.Foreground = ButtonColors[button];

            return fontIcon;
        }
    }
}
