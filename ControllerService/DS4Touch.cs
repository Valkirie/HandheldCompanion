using ControllerCommon;
using ControllerCommon.Controllers;
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

        public static TrackPadTouch TrackPadTouch1 = new(TOUCH0_ID);
        public static TrackPadTouch TrackPadTouch2 = new(TOUCH1_ID);
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
                    TrackPadTouch1.X = TouchX;
                    TrackPadTouch1.Y = TouchY;
                    TrackPadTouch1.RawTrackingNum |= TOUCH_DISABLE;
                    break;
                case CursorButton.TouchRight:
                    TrackPadTouch2.X = TouchX;
                    TrackPadTouch2.Y = TouchY;
                    TrackPadTouch2.RawTrackingNum |= TOUCH_DISABLE;
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
                    TrackPadTouch1.X = TouchX;
                    TrackPadTouch1.Y = TouchY;
                    TrackPadTouch1.RawTrackingNum &= ~TOUCH_DISABLE;
                    break;
                case CursorButton.TouchRight:
                    TrackPadTouch2.X = TouchX;
                    TrackPadTouch2.Y = TouchY;
                    TrackPadTouch2.RawTrackingNum &= ~TOUCH_DISABLE;
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
                    TrackPadTouch1.X = TouchX;
                    TrackPadTouch1.Y = TouchY;
                    break;
                case CursorButton.TouchRight:
                    TrackPadTouch2.X = TouchX;
                    TrackPadTouch2.Y = TouchY;
                    break;
            }
        }

        private static bool prevLeftPadTouch, prevRightPadTouch;
        public static void UpdateInputs(ControllerInput inputs)
        {
            if (prevLeftPadTouch != inputs.LeftPadTouch)
            {
                if (inputs.LeftPadTouch)
                {
                    TouchPacketCounter++;
                    TrackPadTouch1.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    TrackPadTouch1.RawTrackingNum |= TOUCH_DISABLE;
                }
            }

            if (prevRightPadTouch != inputs.RightPadTouch)
            {
                if (inputs.RightPadTouch)
                {
                    TouchPacketCounter++;
                    TrackPadTouch2.RawTrackingNum &= ~TOUCH_DISABLE;
                }
                else
                {
                    TrackPadTouch2.RawTrackingNum |= TOUCH_DISABLE;
                }
            }

            TrackPadTouch1.X = (short)(inputs.LeftPadX * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f);
            TrackPadTouch1.Y = (short)(inputs.LeftPadY * TOUCHPAD_HEIGHT / ushort.MaxValue);

            TrackPadTouch2.X = (short)((inputs.RightPadX * TOUCHPAD_WIDTH / ushort.MaxValue / 2.0f) + (0.5f * TOUCHPAD_WIDTH));
            TrackPadTouch2.Y = (short)(inputs.RightPadY * TOUCHPAD_HEIGHT / ushort.MaxValue);

            OutputClickButton = inputs.LeftPadClick || inputs.RightPadClick;

            prevLeftPadTouch = inputs.LeftPadTouch;
            prevRightPadTouch = inputs.RightPadTouch;
        }
    }
}
