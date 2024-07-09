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
            _buttonState = new Dictionary<NeptuneControllerButton, bool>
            {
                [NeptuneControllerButton.BtnA] = (input.buttons0 & (ushort)NCButton0.BTN_A) == (ushort)NCButton0.BTN_A,
                [NeptuneControllerButton.BtnB] = (input.buttons0 & (ushort)NCButton0.BTN_B) == (ushort)NCButton0.BTN_B,
                [NeptuneControllerButton.BtnX] = (input.buttons0 & (ushort)NCButton0.BTN_X) == (ushort)NCButton0.BTN_X,
                [NeptuneControllerButton.BtnY] = (input.buttons0 & (ushort)NCButton0.BTN_Y) == (ushort)NCButton0.BTN_Y,

                [NeptuneControllerButton.BtnL1] = (input.buttons0 & (ushort)NCButton0.BTN_L1) == (ushort)NCButton0.BTN_L1,
                [NeptuneControllerButton.BtnL2] = (input.buttons0 & (ushort)NCButton0.BTN_L2) == (ushort)NCButton0.BTN_L2,
                [NeptuneControllerButton.BtnR1] = (input.buttons0 & (ushort)NCButton0.BTN_R1) == (ushort)NCButton0.BTN_R1,
                [NeptuneControllerButton.BtnR2] = (input.buttons0 & (ushort)NCButton0.BTN_R2) == (ushort)NCButton0.BTN_R2,

                [NeptuneControllerButton.BtnDpadDown] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_DOWN) == (ushort)NCButton1.BTN_DPAD_DOWN,
                [NeptuneControllerButton.BtnDpadUp] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_UP) == (ushort)NCButton1.BTN_DPAD_UP,
                [NeptuneControllerButton.BtnDpadLeft] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_LEFT) == (ushort)NCButton1.BTN_DPAD_LEFT,
                [NeptuneControllerButton.BtnDpadRight] = (input.buttons1 & (ushort)NCButton1.BTN_DPAD_RIGHT) == (ushort)NCButton1.BTN_DPAD_RIGHT,

                [NeptuneControllerButton.BtnMenu] = (input.buttons1 & (ushort)NCButton1.BTN_MENU) == (ushort)NCButton1.BTN_MENU,
                [NeptuneControllerButton.BtnSteam] = (input.buttons1 & (ushort)NCButton1.BTN_STEAM) == (ushort)NCButton1.BTN_STEAM,
                [NeptuneControllerButton.BtnOptions] = (input.buttons1 & (ushort)NCButton1.BTN_OPTIONS) == (ushort)NCButton1.BTN_OPTIONS,

                [NeptuneControllerButton.BtnL5] = (input.buttons1 & (ushort)NCButton1.BTN_L5) == (ushort)NCButton1.BTN_L5,
                [NeptuneControllerButton.BtnR5] = (input.buttons2 & (ushort)NCButton2.BTN_R5) == (ushort)NCButton2.BTN_R5,

                [NeptuneControllerButton.BtnLPadPress] = (input.buttons2 & (ushort)NCButton2.BTN_LPAD_PRESS) == (ushort)NCButton2.BTN_LPAD_PRESS,
                [NeptuneControllerButton.BtnRPadPress] = (input.buttons2 & (ushort)NCButton2.BTN_RPAD_PRESS) == (ushort)NCButton2.BTN_RPAD_PRESS,
                [NeptuneControllerButton.BtnLPadTouch] = (input.buttons2 & (ushort)NCButton2.BTN_LPAD_TOUCH) == (ushort)NCButton2.BTN_LPAD_TOUCH,
                [NeptuneControllerButton.BtnRPadTouch] = (input.buttons2 & (ushort)NCButton2.BTN_RPAD_TOUCH) == (ushort)NCButton2.BTN_RPAD_TOUCH,

                [NeptuneControllerButton.BtnLStickPress] = (input.buttons2 & (ushort)NCButton2.BTN_LSTICK_PRESS) == (ushort)NCButton2.BTN_LSTICK_PRESS,
                [NeptuneControllerButton.BtnRStickPress] = (input.buttons3 & (ushort)NCButton3.BTN_RSTICK_PRESS) == (ushort)NCButton3.BTN_RSTICK_PRESS,
                [NeptuneControllerButton.BtnLStickTouch] = (input.buttons5 & (ushort)NCButton5.BTN_LSTICK_TOUCH) == (ushort)NCButton5.BTN_LSTICK_TOUCH,
                [NeptuneControllerButton.BtnRStickTouch] = (input.buttons5 & (ushort)NCButton5.BTN_RSTICK_TOUCH) == (ushort)NCButton5.BTN_RSTICK_TOUCH,

                [NeptuneControllerButton.BtnR4] = (input.buttons5 & (ushort)NCButton5.BTN_R4) == (ushort)NCButton5.BTN_R4,
                [NeptuneControllerButton.BtnL4] = (input.buttons5 & (ushort)NCButton5.BTN_L4) == (ushort)NCButton5.BTN_L4,

                [NeptuneControllerButton.BtnQuickAccess] = (input.buttons6 & (ushort)NCButton6.BTN_QUICK_ACCESS) == (ushort)NCButton6.BTN_QUICK_ACCESS
            };
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
            _axisState = new Dictionary<NeptuneControllerAxis, Int16>
            {
                [NeptuneControllerAxis.LeftStickX] = input.lthumb_x,
                [NeptuneControllerAxis.LeftStickY] = input.lthumb_y,
                [NeptuneControllerAxis.RightStickX] = input.rthumb_x,
                [NeptuneControllerAxis.RightStickY] = input.rthumb_y,

                [NeptuneControllerAxis.LeftPadX] = input.lpad_x,
                [NeptuneControllerAxis.LeftPadY] = input.lpad_y,
                [NeptuneControllerAxis.RightPadX] = input.rpad_x,
                [NeptuneControllerAxis.RightPadY] = input.rpad_y,

                [NeptuneControllerAxis.LeftPadPressure] = input.lpad_pressure,
                [NeptuneControllerAxis.RightPadPressure] = input.rpad_pressure,

                [NeptuneControllerAxis.L2] = input.ltrig,
                [NeptuneControllerAxis.R2] = input.rtrig,

                [NeptuneControllerAxis.GyroAccelX] = input.accel_x,
                [NeptuneControllerAxis.GyroAccelY] = input.accel_y,
                [NeptuneControllerAxis.GyroAccelZ] = input.accel_z,

                [NeptuneControllerAxis.GyroYaw] = input.gyaw,
                [NeptuneControllerAxis.GyroRoll] = input.groll,
                [NeptuneControllerAxis.GyroPitch] = input.gpitch,

                [NeptuneControllerAxis.Q1] = input.q1,
                [NeptuneControllerAxis.Q2] = input.q2,
                [NeptuneControllerAxis.Q3] = input.q3,
                [NeptuneControllerAxis.Q4] = input.q4
            };
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
