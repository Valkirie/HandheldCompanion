using System.Windows.Forms;

namespace ControllerService
{
    public class DS4Touch
    {
        private float RatioWidth;
        private float RatioHeight;

        private const int TOUCHPAD_WIDTH = 1920;
        private const int TOUCHPAD_HEIGHT = 943;

        private const int TOUCH0_ID = 126;
        private const int TOUCH1_ID = 127;
        private const int TOUCH_DISABLE = 128;

        public struct TrackPadTouch
        {
            public bool IsActive;
            public byte Id;
            public short X;
            public short Y;
            public byte RawTrackingNum;
        }

        public TrackPadTouch TrackPadTouch0;
        public TrackPadTouch TrackPadTouch1;
        public byte TouchPacketCounter = 0;

        private short TouchX, TouchY;

        public DS4Touch()
        {
            // get screen size
            RatioWidth = (float)TOUCHPAD_WIDTH / (float)Screen.PrimaryScreen.Bounds.Width;
            RatioHeight = (float)TOUCHPAD_HEIGHT / (float)Screen.PrimaryScreen.Bounds.Height;

            // default values
            TrackPadTouch0.RawTrackingNum = TOUCH0_ID;
            TrackPadTouch1.RawTrackingNum = TOUCH1_ID;
        }

        public void OnMouseUp(short X, short Y)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            TrackPadTouch0.RawTrackingNum += TOUCH_DISABLE;
            TrackPadTouch1.RawTrackingNum += TOUCH_DISABLE;

            TouchPacketCounter++;
        }

        public void OnMouseDown(short X, short Y)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            TrackPadTouch0.RawTrackingNum = TOUCH0_ID;
            TrackPadTouch0.X = TouchX;
            TrackPadTouch0.Y = TouchY;

            TrackPadTouch1.RawTrackingNum = TOUCH1_ID;
            TrackPadTouch1.X = TouchX;
            TrackPadTouch1.Y = TouchY;
        }

        public void OnMouseMove(short X, short Y)
        {
            TouchX = (short)(X * RatioWidth);
            TouchY = (short)(Y * RatioHeight);

            switch (TouchPacketCounter % 2)
            {
                case 0:
                    TrackPadTouch0.X = TouchX;
                    TrackPadTouch0.Y = TouchY;
                    break;
                case 1:
                    TrackPadTouch1.X = TouchX;
                    TrackPadTouch1.Y = TouchY;
                    break;
            }

            TouchPacketCounter++;
        }
    }
}
