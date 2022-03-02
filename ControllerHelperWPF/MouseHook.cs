using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using WindowsHook;
using MouseButtons = WindowsHook.MouseButtons;
using Timer = System.Timers.Timer;

namespace ControllerHelperWPF
{
    public class MouseHook
    {
        public struct TouchInput
        {
            public float X;
            public float Y;
            public MouseButtons Button;
            public int Timestamp;
        }

        private IKeyboardMouseEvents m_Events;
        private Thread m_Hook;
        private Timer m_Timer;
        private bool isDoubleClick;

        private readonly PipeClient client;
        private readonly ILogger logger;

        private TouchInput m_MouseUp;
        private ushort m_MouseMove;

        private bool m_MouseEnable;
        private bool m_MouseExclusive;

        public MouseHook(PipeClient client, ILogger logger)
        {
            this.client = client;
            this.logger = logger;

            // send MouseUp after default interval (20ms)
            m_Timer = new Timer() { Enabled = false, Interval = 20, AutoReset = false };
            m_Timer.Elapsed += SendMouseUp;
        }

        public void Start()
        {
            m_Hook = new Thread(Subscribe) { IsBackground = true };
            m_Hook.Start();

            logger.LogInformation("Mouse hook has started");
        }

        public void SetInterval(double ms)
        {
            m_Timer.Interval = ms * 4;
            logger.LogInformation("Mouse hook interval set to {0}", m_Timer.Interval);
        }

        private void Subscribe()
        {
            m_Events = Hook.GlobalEvents();
            m_Events.MouseDownExt += OnMouseDownExt;
            m_Events.MouseUpExt += OnMouseUpExt;

            Application.Run();
        }

        private void SendMouseUp(object sender, ElapsedEventArgs e)
        {
            client.SendMessage(new PipeClientCursor
            {
                action = CursorAction.CursorUp,
                x = m_MouseUp.X,
                y = m_MouseUp.Y,
                button = m_MouseUp.Button
            });
        }

        private void OnMouseDownExt(object sender, MouseEventExtArgs e)
        {
            if (m_Events == null || !m_MouseEnable)
                return;

            m_Timer.Stop();

            var dist = Math.Abs(e.X - m_MouseUp.X);
            var diff = e.Timestamp - m_MouseUp.Timestamp;

            if (m_MouseUp.Button == e.Button)
            {
                if (diff < 200 && dist < 20)
                    isDoubleClick = true;
            }

            m_Events.MouseMoveExt += OnMouseMove;

            client.SendMessage(new PipeClientCursor
            {
                action = CursorAction.CursorDown,
                x = e.X,
                y = e.Y,
                button = isDoubleClick ? MouseButtons.Right : e.Button
            });

            logger.LogDebug("OnMouseDown x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);

            e.Handled = m_MouseExclusive;
        }

        private void OnMouseMove(object sender, MouseEventExtArgs e)
        {
            if (m_Events == null || !m_MouseEnable)
                return;

            m_MouseMove++;

            // reduce CPU usage by moving pointer every 10 px
            if (m_MouseMove % 10 != 0)
                return;

            client.SendMessage(new PipeClientCursor
            {
                action = CursorAction.CursorMove,
                x = e.X,
                y = e.Y,
                button = e.Button
            });

            logger.LogDebug("OnMouseMove x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);
        }

        private void OnMouseUpExt(object sender, MouseEventExtArgs e)
        {
            if (m_Events == null || !m_MouseEnable)
                return;

            m_Events.MouseMoveExt -= OnMouseMove;

            m_MouseUp = new TouchInput()
            {
                X = e.X,
                Y = e.Y,
                Button = e.Button,
                Timestamp = e.Timestamp
            };
            logger.LogDebug("OnMouseUp x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);

            m_Timer.Start();

            isDoubleClick = false;
        }

        public void Stop()
        {
            if (m_Events == null)
                return;

            m_Events.MouseDownExt -= OnMouseDownExt;
            m_Events.MouseUpExt -= OnMouseUpExt;
            m_Events.MouseMoveExt -= OnMouseMove;
            m_Events.Dispose();
            m_Events = null;

            logger.LogInformation("Mouse hook has stopped");
        }

        public void UpdateProfile(Profile profile)
        {
            m_MouseExclusive = profile.mousehook_exclusive;
            m_MouseEnable = profile.mousehook_enabled;
        }
    }
}
