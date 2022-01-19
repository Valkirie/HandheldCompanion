using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using WindowsHook;
using MouseButtons = WindowsHook.MouseButtons;
using Timer = System.Timers.Timer;

namespace ControllerHelper
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

        public bool hooked;

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
            hooked = true;

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
            m_Events.MouseDownExt += OnMouseDown;
            m_Events.MouseUpExt += OnMouseUp;
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

        private void OnMouseDown(object sender, MouseEventExtArgs e)
        {
            if (m_Events == null)
                return;

            m_Timer.Stop();

            var dist = Math.Abs(e.X - m_MouseUp.X);
            var diff = e.Timestamp - m_MouseUp.Timestamp;

            if (m_MouseUp.Button == e.Button)
            {
                if (diff < SystemInformation.DoubleClickTime && dist < SystemInformation.DoubleClickSize.Width * 5)
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
        }

        private void OnMouseMove(object sender, MouseEventExtArgs e)
        {
            client.SendMessage(new PipeClientCursor
            {
                action = CursorAction.CursorMove,
                x = e.X,
                y = e.Y,
                button = e.Button
            });

            logger.LogDebug("OnMouseMove x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);
        }

        private void OnMouseUp(object sender, MouseEventExtArgs e)
        {
            if (m_Events == null)
                return;

            m_Events.MouseMoveExt -= OnMouseMove;

            m_MouseUp = new TouchInput()
            {
                X = e.X,
                Y = e.Y,
                Button = isDoubleClick ? MouseButtons.Right : e.Button,
                Timestamp = e.Timestamp
            };
            logger.LogDebug("OnMouseUp x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);

            m_Timer.Start();

            isDoubleClick = false;
        }

        internal void Stop()
        {
            if (m_Events == null)
                return;

            m_Events.MouseDownExt -= OnMouseDown;
            m_Events.MouseUpExt -= OnMouseUp;
            m_Events.Dispose();
            m_Events = null;
            hooked = false;

            logger.LogInformation("Mouse hook has stopped");
        }
    }
}
