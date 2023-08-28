using steam_hidapi.net.Hid;
using steam_hidapi.net.Util;
using System;
using System.Collections.Generic;


namespace steam_hidapi.net
{
    // TODO: wired controller has more bytes usable in its packet, use those as well?

    public class GordonControllerInputState
    {
        public GordonControllerButtonState ButtonState { get; private set; }
        public GordonControllerAxesState AxesState { get; private set; }
        internal GordonControllerInputState(GCInput input)
        {
            ButtonState = new GordonControllerButtonState(input);
            AxesState = new GordonControllerAxesState(input);
        }
    }

    public class GordonControllerButtonState
    {
        private Dictionary<GordonControllerButton, bool> _buttonState;

        public GordonControllerButtonState(Dictionary<GordonControllerButton, bool> buttonState)
        {
            _buttonState = buttonState;
        }
        internal GordonControllerButtonState(GCInput input)
        {
            _buttonState = new Dictionary<GordonControllerButton, bool>();

            _buttonState[GordonControllerButton.BtnA] = (input.buttons0 & (byte)GCButton0.BTN_A) == (byte)GCButton0.BTN_A;
            _buttonState[GordonControllerButton.BtnB] = (input.buttons0 & (byte)GCButton0.BTN_B) == (byte)GCButton0.BTN_B;
            _buttonState[GordonControllerButton.BtnX] = (input.buttons0 & (byte)GCButton0.BTN_X) == (byte)GCButton0.BTN_X;
            _buttonState[GordonControllerButton.BtnY] = (input.buttons0 & (byte)GCButton0.BTN_Y) == (byte)GCButton0.BTN_Y;

            _buttonState[GordonControllerButton.BtnL1] = (input.buttons0 & (byte)GCButton0.BTN_L1) == (byte)GCButton0.BTN_L1;
            _buttonState[GordonControllerButton.BtnL2] = (input.buttons0 & (byte)GCButton0.BTN_L2) == (byte)GCButton0.BTN_L2;
            _buttonState[GordonControllerButton.BtnR1] = (input.buttons0 & (byte)GCButton0.BTN_R1) == (byte)GCButton0.BTN_R1;
            _buttonState[GordonControllerButton.BtnR2] = (input.buttons0 & (byte)GCButton0.BTN_R2) == (byte)GCButton0.BTN_R2;

            _buttonState[GordonControllerButton.BtnDpadDown] = (input.buttons1 & (byte)GCButton1.BTN_DPAD_DOWN) == (byte)GCButton1.BTN_DPAD_DOWN;
            _buttonState[GordonControllerButton.BtnDpadUp] = (input.buttons1 & (byte)GCButton1.BTN_DPAD_UP) == (byte)GCButton1.BTN_DPAD_UP;
            _buttonState[GordonControllerButton.BtnDpadLeft] = (input.buttons1 & (byte)GCButton1.BTN_DPAD_LEFT) == (byte)GCButton1.BTN_DPAD_LEFT;
            _buttonState[GordonControllerButton.BtnDpadRight] = (input.buttons1 & (byte)GCButton1.BTN_DPAD_RIGHT) == (byte)GCButton1.BTN_DPAD_RIGHT;

            _buttonState[GordonControllerButton.BtnMenu] = (input.buttons1 & (byte)GCButton1.BTN_MENU) == (byte)GCButton1.BTN_MENU;
            _buttonState[GordonControllerButton.BtnSteam] = (input.buttons1 & (byte)GCButton1.BTN_STEAM) == (byte)GCButton1.BTN_STEAM;
            _buttonState[GordonControllerButton.BtnOptions] = (input.buttons1 & (byte)GCButton1.BTN_OPTIONS) == (byte)GCButton1.BTN_OPTIONS;

            _buttonState[GordonControllerButton.BtnL4] = (input.buttons1 & (byte)GCButton1.BTN_L4) == (byte)GCButton1.BTN_L4;
            _buttonState[GordonControllerButton.BtnR4] = (input.buttons2 & (byte)GCButton2.BTN_R4) == (byte)GCButton2.BTN_R4;

            bool lpad_touched = (input.buttons2 & (byte)GCButton2.BTN_LPAD_TOUCH) == (byte)GCButton2.BTN_LPAD_TOUCH;
            bool lpad_and_joy = (input.buttons2 & (byte)GCButton2.BTN_LPAD_AND_JOY) == (byte)GCButton2.BTN_LPAD_AND_JOY;

            _buttonState[GordonControllerButton.BtnLPadPress] = (lpad_touched || lpad_and_joy) && (input.buttons2 & (byte)GCButton2.BTN_LPAD_PRESS) == (byte)GCButton2.BTN_LPAD_PRESS;
            _buttonState[GordonControllerButton.BtnRPadPress] = (input.buttons2 & (byte)GCButton2.BTN_RPAD_PRESS) == (byte)GCButton2.BTN_RPAD_PRESS;
            _buttonState[GordonControllerButton.BtnLPadTouch] = lpad_touched || lpad_and_joy;
            _buttonState[GordonControllerButton.BtnRPadTouch] = (input.buttons2 & (byte)GCButton2.BTN_RPAD_TOUCH) == (byte)GCButton2.BTN_RPAD_TOUCH;
            _buttonState[GordonControllerButton.BtnLStickPress] = (input.buttons2 & (byte)GCButton2.BTN_LSTICK_PRESS) == (byte)GCButton2.BTN_LSTICK_PRESS;
        }

        public bool this[GordonControllerButton button]
        {
            get
            {
                return _buttonState.ContainsKey(button) ? _buttonState[button] : false;
            }
        }

        public IEnumerable<GordonControllerButton> Buttons => _buttonState.Keys;

        public override bool Equals(object obj)
        {
            return obj is GordonControllerButtonState state &&
                   _buttonState.EqualsWithValues(state._buttonState);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class GordonControllerAxesState
    {
        private Dictionary<GordonControllerAxis, Int16> _axisState;

        public GordonControllerAxesState(Dictionary<GordonControllerAxis, Int16> axisState)
        {
            _axisState = axisState;
        }

        internal GordonControllerAxesState(GCInput input)
        {
            _axisState = new Dictionary<GordonControllerAxis, Int16>();

            // TODO: this logic is for wireless, wired should report those directly
            bool lpad_touched = (input.buttons2 & (byte)GCButton2.BTN_LPAD_TOUCH) == (byte)GCButton2.BTN_LPAD_TOUCH;
            bool lpad_and_joy = (input.buttons2 & (byte)GCButton2.BTN_LPAD_AND_JOY) == (byte)GCButton2.BTN_LPAD_AND_JOY;
            if (lpad_touched)
            {
                _axisState[GordonControllerAxis.LeftPadX] = input.lpad_x;
                _axisState[GordonControllerAxis.LeftPadY] = input.lpad_y;
            }
            else
            {
                _axisState[GordonControllerAxis.LeftStickX] = input.lpad_x;
                _axisState[GordonControllerAxis.LeftStickY] = input.lpad_y;
            }
            if (lpad_touched && !lpad_and_joy)
            {
                _axisState[GordonControllerAxis.LeftStickX] = 0;
                _axisState[GordonControllerAxis.LeftStickY] = 0;
            }
            if (lpad_touched && lpad_and_joy)
            {
                _axisState[GordonControllerAxis.LeftPadX] = 0;
                _axisState[GordonControllerAxis.LeftPadY] = 0;
            }

            _axisState[GordonControllerAxis.RightPadX] = input.rpad_x;
            _axisState[GordonControllerAxis.RightPadY] = input.rpad_y;

            _axisState[GordonControllerAxis.L2] = input.ltrig; // byte
            _axisState[GordonControllerAxis.R2] = input.rtrig; // byte

            _axisState[GordonControllerAxis.GyroAccelX] = input.accel_x;
            _axisState[GordonControllerAxis.GyroAccelY] = input.accel_y;
            _axisState[GordonControllerAxis.GyroAccelZ] = input.accel_z;

            _axisState[GordonControllerAxis.GyroYaw] = input.gyaw;
            _axisState[GordonControllerAxis.GyroRoll] = input.groll;
            _axisState[GordonControllerAxis.GyroPitch] = input.gpitch;

            _axisState[GordonControllerAxis.Q1] = input.q1;
            _axisState[GordonControllerAxis.Q2] = input.q2;
            _axisState[GordonControllerAxis.Q3] = input.q3;
            _axisState[GordonControllerAxis.Q4] = input.q4;
        }

        public Int16 this[GordonControllerAxis axis]
        {
            get
            {
                return _axisState.ContainsKey(axis) ? _axisState[axis] : (Int16)0;
            }
        }

        public IEnumerable<GordonControllerAxis> Axes => _axisState.Keys;
    }
}
