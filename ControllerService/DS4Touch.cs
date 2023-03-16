using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;

namespace ControllerService
{
    public static class DS4Touch
    {
        private const int TOUCHPAD_WIDTH = 1920;
        private const int TOUCHPAD_HEIGHT = 943;

        private const int TOUCH0_ID = 1;
        private const int TOUCH1_ID = 2;
        private const int TOUCH_DISABLE = 128;

        public class TrackPadTouch
        {
            public bool IsActive;
            public byte Id;
            public short X;
            public short Y;
            public int RawTrackingNum;

            public TrackPadTouch(byte _Id)
            {
                this.Id = _Id;
                this.RawTrackingNum |= _Id + TOUCH_DISABLE;
            }
        }

        public static TrackPadTouch LeftPadTouch = new(TOUCH0_ID);
        public static TrackPadTouch RightPadTouch = new(TOUCH1_ID);
        public static byte TouchPacketCounter = 0;

        private static short TouchX, TouchY;
        public static bool OutputClickButton;

        public static void OnMouseUp(double X, double Y, CursorButton Button, int flags = 20)
        {
            TouchX = (short)(X * TOUCHPAD_WIDTH);
            TouchY = (short)(Y * TOUCHPAD_HEIGHT);

            switch (Button)
            {
                case CursorButton.TouchLeft:
                    LeftPadTouch.X = TouchX;
                    LeftPadTouch.Y = TouchY;
                    LeftPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                    break;
                case CursorButton.TouchRight:
                    RightPadTouch.X = TouchX;
                    RightPadTouch.Y = TouchY;
                    RightPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                    break;
            }

            OutputClickButton = false;
        }

        public static void OnMouseDown(double X, double Y, CursorButton Button, int flags = 20)
        {
            TouchX = (short)(X * TOUCHPAD_WIDTH);
            TouchY = (short)(Y * TOUCHPAD_HEIGHT);

            switch (Button)
            {
                case CursorButton.TouchLeft:
                    LeftPadTouch.X = TouchX;
                    LeftPadTouch.Y = TouchY;
                    LeftPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                    break;
                case CursorButton.TouchRight:
                    RightPadTouch.X = TouchX;
                    RightPadTouch.Y = TouchY;
                    RightPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                    break;
            }

            if (flags > 26) // double tap
                OutputClickButton = true;

            TouchPacketCounter++;
        }

        public static void OnMouseMove(double X, double Y, CursorButton Button, int flags = 20)
        {
            TouchX = (short)(X * TOUCHPAD_WIDTH);
            TouchY = (short)(Y * TOUCHPAD_HEIGHT);

            switch (Button)
            {
                case CursorButton.TouchLeft:
                    LeftPadTouch.X = TouchX;
                    LeftPadTouch.Y = TouchY;
                    break;
                case CursorButton.TouchRight:
                    RightPadTouch.X = TouchX;
                    RightPadTouch.Y = TouchY;
                    break;
            }
        }

        private static bool prevLeftPadTouch, prevRightPadTouch;
        private static bool prevLeftPadClick, prevRightPadClick;
        public static void UpdateInputs(ControllerState Inputs)
        {
            if (prevLeftPadTouch != Inputs.ButtonState[ButtonFlags.LeftPadTouch])
            {
                if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
                {
                    TouchPacketCounter++;
                    LeftPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    LeftPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                }

                prevLeftPadTouch = Inputs.ButtonState[ButtonFlags.LeftPadTouch];
            }

            if (prevRightPadTouch != Inputs.ButtonState[ButtonFlags.RightPadTouch])
            {
                if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
                {
                    TouchPacketCounter++;
                    RightPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    RightPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                }

                prevRightPadTouch = Inputs.ButtonState[ButtonFlags.RightPadTouch];
            }

            if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
            {
                LeftPadTouch.X = (short)((Inputs.AxisState[AxisFlags.LeftPadX] + short.MaxValue) * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f);
                LeftPadTouch.Y = (short)((-Inputs.AxisState[AxisFlags.LeftPadY] + short.MaxValue) * TOUCHPAD_HEIGHT / ushort.MaxValue);
            }

            if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
            {
                RightPadTouch.X = (short)(((Inputs.AxisState[AxisFlags.RightPadX] + short.MaxValue) * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f) + (0.5f * TOUCHPAD_WIDTH));
                RightPadTouch.Y = (short)((-Inputs.AxisState[AxisFlags.RightPadY] + short.MaxValue) * TOUCHPAD_HEIGHT / ushort.MaxValue);
            }

            if (prevLeftPadClick != Inputs.ButtonState[ButtonFlags.LeftPadClick] || prevRightPadClick != Inputs.ButtonState[ButtonFlags.RightPadClick])
            {
                if (Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick])
                    OutputClickButton = true;
                else
                    OutputClickButton = false;

                prevLeftPadClick = Inputs.ButtonState[ButtonFlags.LeftPadClick];
                prevRightPadClick = Inputs.ButtonState[ButtonFlags.RightPadClick];
            }
        }
    }
}
