using System.Windows.Forms;
using MouseButtons = WindowsHook.MouseButtons;

namespace ControllerService
{
    public class DS4Touch
    {
        private float RatioWidth;
        private float RatioHeight;

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
            // default ratio (not dpi aware)
            UpdateRatio(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

            // default values
            TrackPadTouch1.RawTrackingNum |= TOUCH0_ID + TOUCH_DISABLE;
            TrackPadTouch2.RawTrackingNum |= TOUCH1_ID + TOUCH_DISABLE;
        }

        public void UpdateRatio(float w, float h)
        {
            BoundsWidth = w;
            BoundsHeight = h;

            RatioWidth = (float)TOUCHPAD_WIDTH / BoundsWidth;
            RatioHeight = (float)TOUCHPAD_HEIGHT / BoundsHeight;
        }

        public void OnMouseUp(short X, short Y, MouseButtons Button)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            TrackPadTouch1.X = TouchX;
            TrackPadTouch1.Y = TouchY;

            // TrackPadTouch2.X = TouchX;
            // TrackPadTouch2.Y = TouchY;

            OutputClickButton = false;

            TrackPadTouch1.RawTrackingNum |= TOUCH_DISABLE;
            // TrackPadTouch2.RawTrackingNum |= TOUCH_DISABLE;
        }

        public void OnMouseDown(short X, short Y, MouseButtons Button)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            TrackPadTouch1.X = TouchX;
            TrackPadTouch1.Y = TouchY;

            // TrackPadTouch2.X = TouchX;
            // TrackPadTouch2.Y = TouchY;

            if (Button == MouseButtons.Right)
                OutputClickButton = true;

            TrackPadTouch1.RawTrackingNum &= ~TOUCH_DISABLE;
            // TrackPadTouch2.RawTrackingNum &= ~TOUCH_DISABLE;

            TouchPacketCounter++;
        }

        public void OnMouseMove(short X, short Y, MouseButtons Button)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            TrackPadTouch1.X = TouchX;
            TrackPadTouch1.Y = TouchY;

            // TrackPadTouch2.X = TouchX;
            // TrackPadTouch2.Y = TouchY;
        }
    }
}
