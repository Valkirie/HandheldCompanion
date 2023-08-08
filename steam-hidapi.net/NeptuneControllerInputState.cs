using steam_hidapi.net.Hid;
using steam_hidapi.net.Util;
using System;
using System.Collections.Generic;


namespace steam_hidapi.net
{
    public class NeptuneControllerInputState
    {
        public NeptuneControllerButtonState ButtonState { get; private set; }
        public NeptuneControllerAxesState AxesState { get; private set; }
        internal NeptuneControllerInputState(NCInput input)
        {
            ButtonState = new NeptuneControllerButtonState(input);
            AxesState = new NeptuneControllerAxesState(input);
        }
    }

    public class NeptuneControllerButtonState
    {
        private Dictionary<NeptuneControllerButton, bool> _buttonState;

        public NeptuneControllerButtonState(Dictionary<NeptuneControllerButton, bool> buttonState)
        {
            _buttonState = buttonState;
        }
        internal NeptuneControllerButtonState(NCInput input)
        {
            _buttonState = new Dictionary<NeptuneControllerButton, bool>();

            _buttonState[NeptuneControllerButton.BtnA] = (input.buttons0 & (ushort)NCButton0.BTN_A) == (ushort)NCButton0.BTN_A;
            _buttonState[NeptuneControllerButton.BtnB] = (input.buttons0 & (ushort)NCButton0.BTN_B) == (ushort)NCButton0.BTN_B;
            _buttonState[NeptuneControllerButton.BtnX] = (input.buttons0 & (ushort)NCButton0.BTN_X) == (ushort)NCButton0.BTN_X;
            _buttonState[NeptuneControllerButton.BtnY] = (input.buttons0 & (ushort)NCButton0.BTN_Y) == (ushort)NCButton0.BTN_Y;

            _buttonState[NeptuneControllerButton.BtnL1] = (input.buttons0 & (ushort)NCButton0.BTN_L1) == (ushort)NCButton0.BTN_L1;
            _buttonState[NeptuneControllerButton.BtnL2] = (input.buttons0 & (ushort)NCButton0.BTN_L2) == (ushort)NCButton0.BTN_L2;
            _buttonState[NeptuneControllerButton.BtnR1] = (input.buttons0 & (ushort)NCButton0.BTN_R1) == (ushort)NCButton0.BTN_R1;
            _buttonState[NeptuneControllerButton.BtnR2] = (input.buttons0 & (ushort)NCButton0.BTN_R2) == (ushort)NCButton0.BTN_R2;

            _buttonState[NeptuneControllerButton.BtnDpadDown] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_DOWN) == (ushort)NCButton1.BTN_DPAD_DOWN;
            _buttonState[NeptuneControllerButton.BtnDpadUp] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_UP) == (ushort)NCButton1.BTN_DPAD_UP;
            _buttonState[NeptuneControllerButton.BtnDpadLeft] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_LEFT) == (ushort)NCButton1.BTN_DPAD_LEFT;
            _buttonState[NeptuneControllerButton.BtnDpadRight] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_RIGHT) == (ushort)NCButton1.BTN_DPAD_RIGHT;

            _buttonState[NeptuneControllerButton.BtnMenu] = (input.buttons1 & (ushort)NCButton1.BTN_MENU) == (ushort)NCButton1.BTN_MENU;
            _buttonState[NeptuneControllerButton.BtnSteam] = (input.buttons1 & (ushort)NCButton1.BTN_STEAM) == (ushort)NCButton1.BTN_STEAM;
            _buttonState[NeptuneControllerButton.BtnOptions] = (input.buttons1 & (ushort)NCButton1.BTN_OPTIONS) == (ushort)NCButton1.BTN_OPTIONS;

            _buttonState[NeptuneControllerButton.BtnL5] = (input.buttons1 & (ushort)NCButton1.BTN_L5) == (ushort)NCButton1.BTN_L5;
            _buttonState[NeptuneControllerButton.BtnR5] = (input.buttons2 & (ushort)NCButton2.BTN_R5) == (ushort)NCButton2.BTN_R5;

            _buttonState[NeptuneControllerButton.BtnLPadPress] = (input.buttons2 & (ushort)NCButton2.BTN_LPAD_PRESS) == (ushort)NCButton2.BTN_LPAD_PRESS;
            _buttonState[NeptuneControllerButton.BtnRPadPress] = (input.buttons2 & (ushort)NCButton2.BTN_RPAD_PRESS) == (ushort)NCButton2.BTN_RPAD_PRESS;
            _buttonState[NeptuneControllerButton.BtnLPadTouch] = (input.buttons2 & (ushort)NCButton2.BTN_LPAD_TOUCH) == (ushort)NCButton2.BTN_LPAD_TOUCH;
            _buttonState[NeptuneControllerButton.BtnRPadTouch] = (input.buttons2 & (ushort)NCButton2.BTN_RPAD_TOUCH) == (ushort)NCButton2.BTN_RPAD_TOUCH;

            _buttonState[NeptuneControllerButton.BtnLStickPress] = (input.buttons2 & (ushort)NCButton2.BTN_LSTICK_PRESS) == (ushort)NCButton2.BTN_LSTICK_PRESS;
            _buttonState[NeptuneControllerButton.BtnRStickPress] = (input.buttons3 & (ushort)NCButton3.BTN_RSTICK_PRESS) == (ushort)NCButton3.BTN_RSTICK_PRESS;
            _buttonState[NeptuneControllerButton.BtnLStickTouch] = (input.buttons5 & (ushort)NCButton5.BTN_LSTICK_TOUCH) == (ushort)NCButton5.BTN_LSTICK_TOUCH;
            _buttonState[NeptuneControllerButton.BtnRStickTouch] = (input.buttons5 & (ushort)NCButton5.BTN_RSTICK_TOUCH) == (ushort)NCButton5.BTN_RSTICK_TOUCH;

            _buttonState[NeptuneControllerButton.BtnR4] = (input.buttons5 & (ushort)NCButton5.BTN_R4) == (ushort)NCButton5.BTN_R4;
            _buttonState[NeptuneControllerButton.BtnL4] = (input.buttons5 & (ushort)NCButton5.BTN_L4) == (ushort)NCButton5.BTN_L4;

            _buttonState[NeptuneControllerButton.BtnQuickAccess] = (input.buttons6 & (ushort)NCButton6.BTN_QUICK_ACCESS) == (ushort)NCButton6.BTN_QUICK_ACCESS;
        }

        public bool this[NeptuneControllerButton button]
        {
            get
            {
                return _buttonState.ContainsKey(button) ? _buttonState[button] : false;
            }
        }

        public IEnumerable<NeptuneControllerButton> Buttons => _buttonState.Keys;

        public override bool Equals(object obj)
        {
            return obj is NeptuneControllerButtonState state &&
                   _buttonState.EqualsWithValues(state._buttonState);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
    public class NeptuneControllerAxesState
    {
        private Dictionary<NeptuneControllerAxis, Int16> _axisState;

        public NeptuneControllerAxesState(Dictionary<NeptuneControllerAxis, Int16> axisState)
        {
            _axisState = axisState;
        }

        internal NeptuneControllerAxesState(NCInput input)
        {
            _axisState = new Dictionary<NeptuneControllerAxis, Int16>();

            _axisState[NeptuneControllerAxis.LeftStickX] = input.lthumb_x;
            _axisState[NeptuneControllerAxis.LeftStickY] = input.lthumb_y;
            _axisState[NeptuneControllerAxis.RightStickX] = input.rthumb_x;
            _axisState[NeptuneControllerAxis.RightStickY] = input.rthumb_y;

            _axisState[NeptuneControllerAxis.LeftPadX] = input.lpad_x;
            _axisState[NeptuneControllerAxis.LeftPadY] = input.lpad_y;
            _axisState[NeptuneControllerAxis.RightPadX] = input.rpad_x;
            _axisState[NeptuneControllerAxis.RightPadY] = input.rpad_y;

            _axisState[NeptuneControllerAxis.LeftPadPressure] = input.lpad_pressure;
            _axisState[NeptuneControllerAxis.RightPadPressure] = input.rpad_pressure;

            _axisState[NeptuneControllerAxis.L2] = input.ltrig;
            _axisState[NeptuneControllerAxis.R2] = input.rtrig;

            _axisState[NeptuneControllerAxis.GyroAccelX] = input.accel_x;
            _axisState[NeptuneControllerAxis.GyroAccelY] = input.accel_y;
            _axisState[NeptuneControllerAxis.GyroAccelZ] = input.accel_z;

            _axisState[NeptuneControllerAxis.GyroYaw] = input.gyaw;
            _axisState[NeptuneControllerAxis.GyroRoll] = input.groll;
            _axisState[NeptuneControllerAxis.GyroPitch] = input.gpitch;

            _axisState[NeptuneControllerAxis.Q1] = input.q1;
            _axisState[NeptuneControllerAxis.Q2] = input.q2;
            _axisState[NeptuneControllerAxis.Q3] = input.q3;
            _axisState[NeptuneControllerAxis.Q4] = input.q4;
        }

        public Int16 this[NeptuneControllerAxis axis]
        {
            get
            {
                return _axisState.ContainsKey(axis) ? _axisState[axis] : (Int16)0;
            }
        }

        public IEnumerable<NeptuneControllerAxis> Axes => _axisState.Keys;
    }
}
