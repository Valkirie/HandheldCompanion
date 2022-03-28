using ControllerCommon;
using System.Windows.Forms;

namespace ControllerService
{
    public class DS4Touch
    {
        private const int TOUCHPAD_WIDTH = 1920;
        private const int TOUCHPAD_HEIGHT = 943;

        private const int TOUCH0_ID = 1;
        private const int TOUCH1_ID = 2;
        private const int TOUCH_DISABLE = 128;

        public struct TrackPadTouch
        {
            public bool IsActive;
            public byte Id;
            public short X;
            public short Y;
            public int RawTrackingNum;
        }

        public TrackPadTouch TrackPadTouch1;
        public TrackPadTouch TrackPadTouch2;
        public byte TouchPacketCounter = 0;

        private short TouchX, TouchY;
        public bool OutputClickButton;

        private float BoundsWidth, BoundsHeight;

        public DS4Touch()
        {
            // default values
            TrackPadTouch1.RawTrackingNum |= TOUCH0_ID + TOUCH_DISABLE;
            TrackPadTouch2.RawTrackingNum |= TOUCH1_ID + TOUCH_DISABLE;
        }

        public void OnMouseUp(double X, double Y, CursorButton Button, int flags = 20)
        {
            TouchX = (short)(X * TOUCHPAD_WIDTH);
            TouchY = (short)(Y * TOUCHPAD_HEIGHT);

            switch(Button)
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

        public void OnMouseDown(double X, double Y, CursorButton Button, int flags = 20)
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

        public void OnMouseMove(double X, double Y, CursorButton Button, int flags = 20)
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
