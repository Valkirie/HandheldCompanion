using ControllerCommon;

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

        private static float BoundsWidth, BoundsHeight;

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
    }
}
