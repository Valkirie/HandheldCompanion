using System;
using System.Runtime.InteropServices;

namespace ControllerCommon
{
    public class MultimediaTimer
    {
        private delegate void TimerEventHandler(int id, int msg, IntPtr user, int dw1, int dw2);
        private TimerEventHandler handler;

        public event EventHandler Tick;

        private const int TIME_PERIODIC = 1;
        private const int EVENT_TYPE = TIME_PERIODIC;

        [DllImport("winmm.dll")]
        private static extern int timeSetEvent(int delay, int resolution, TimerEventHandler handler, IntPtr user, int eventType);

        [DllImport("winmm.dll")]
        private static extern int timeKillEvent(int id);

        [DllImport("winmm.dll")]
        private static extern int timeBeginPeriod(int msec);

        [DllImport("winmm.dll")]
        private static extern int timeEndPeriod(int msec);

        public bool AutoReset = true;
        public int Interval = 33;
        private int timerID = 0;

        public MultimediaTimer()
        {
        }

        public MultimediaTimer(int Interval)
        {
            this.Interval = Interval;
        }

        public void Start()
        {
            if (timerID != 0) return;

            timeBeginPeriod(1);
            handler = new TimerEventHandler(TimerHandler);
            timerID = timeSetEvent(Interval, 0, handler, IntPtr.Zero, EVENT_TYPE);
        }

        private void TimerHandler(int id, int msg, IntPtr user, int dw1, int dw2)
        {
            if (Tick != null)
                Tick(this, new EventArgs());

            if (!AutoReset)
                Stop();
        }

        public void Stop()
        {
            if (timerID == 0) return;

            int err = timeKillEvent(timerID);
            timeEndPeriod(1);
            timerID = 0;
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        public bool Enabled
        {
            get
            {
                return timerID != 0;
            }
        }
    }
}
