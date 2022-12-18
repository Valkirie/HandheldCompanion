using ControllerCommon;
using Windows.Devices.Sensors;

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
        public static void UpdateInputs(ControllerInput inputs)
        {
            if (prevLeftPadTouch != inputs.LeftPadTouch)
            {
                if (inputs.LeftPadTouch)
                {
                    TouchPacketCounter++;
                    LeftPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    LeftPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                }

                prevLeftPadTouch = inputs.LeftPadTouch;
            }

            if (prevRightPadTouch != inputs.RightPadTouch)
            {
                if (inputs.RightPadTouch)
                {
                    TouchPacketCounter++;
                    RightPadTouch.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    RightPadTouch.RawTrackingNum |= TOUCH_DISABLE;
                }

                prevRightPadTouch = inputs.RightPadTouch;
            }

            if (inputs.LeftPadTouch)
            {
                LeftPadTouch.X = (short)(inputs.LeftPadX * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f);
                LeftPadTouch.Y = (short)(inputs.LeftPadY * TOUCHPAD_HEIGHT / ushort.MaxValue);
            }

            if (inputs.RightPadTouch)
            {
                RightPadTouch.X = (short)((inputs.RightPadX * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f) + (0.5f * TOUCHPAD_WIDTH));
                RightPadTouch.Y = (short)(inputs.RightPadY * TOUCHPAD_HEIGHT / ushort.MaxValue);
            }

            if (prevLeftPadClick != inputs.LeftPadClick || prevRightPadClick != inputs.RightPadClick)
            {
                if (inputs.LeftPadClick || inputs.RightPadClick)
                    OutputClickButton = true;
                else
                    OutputClickButton = false;

                prevLeftPadClick = inputs.LeftPadClick;
                prevRightPadClick = inputs.RightPadClick;
            }
        }
    }
}
