using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    public class DS4Touch
    {
        private IKeyboardMouseEvents m_Events;
        private Thread m_Hook;

        private float RatioWidth;
        private float RatioHeight;

        private const int TOUCHPAD_WIDTH = 1920;
        private const int TOUCHPAD_HEIGHT = 943;

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
        private bool TouchDown;

        public DS4Touch()
        {
            // get screen size
            RatioWidth = (float)TOUCHPAD_WIDTH / (float)Screen.PrimaryScreen.Bounds.Width;
            RatioHeight = (float)TOUCHPAD_HEIGHT / (float)Screen.PrimaryScreen.Bounds.Height;

            // default values
            TrackPadTouch0.RawTrackingNum = 126;
            TrackPadTouch1.RawTrackingNum = 127;

            m_Hook = new Thread(Subscribe) { IsBackground = true };
            m_Hook.Start();
        }

        private void Subscribe()
        {
            m_Events = Hook.GlobalEvents();
            m_Events.MouseMoveExt += OnMouseMove;
            m_Events.MouseDownExt += OnMouseDown;
            m_Events.MouseUpExt += OnMouseUp;
            Application.Run();
        }

        private void OnMouseUp(object sender, MouseEventExtArgs e)
        {
            TouchDown = false;

            // release touch inputs
            TrackPadTouch0.RawTrackingNum += 128;
            TrackPadTouch1.RawTrackingNum += 128;
        }

        private void OnMouseDown(object sender, MouseEventExtArgs e)
        {
            TouchDown = true;

            TrackPadTouch0.RawTrackingNum = 126;
            TrackPadTouch0.X = TouchX;
            TrackPadTouch0.Y = TouchY;

            TrackPadTouch1.RawTrackingNum = 127;
            TrackPadTouch1.X = TouchX;
            TrackPadTouch1.Y = TouchY;
        }

        private void OnMouseMove(object sender, MouseEventExtArgs e)
        {
            TouchX = (short)(e.X * RatioWidth);
            TouchY = (short)(e.Y * RatioHeight);

            if (!TouchDown)
                return;

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

        internal void Stop()
        {
            m_Events.Dispose();
            m_Hook = null;
        }
    }
}
