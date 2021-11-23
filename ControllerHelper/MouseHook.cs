using ControllerService;
using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
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
        }

        private IKeyboardMouseEvents m_Events;
        private Thread m_Hook;
        private Timer m_Timer;

        private PipeClient client;
        private TouchInput TouchPos;

        public MouseHook(PipeClient client)
        {
            this.client = client;

            // send MouseUp after default interval (40ms)
            m_Timer = new Timer() { Enabled = false, Interval = 40, AutoReset = false };
            m_Timer.Elapsed += SendMouseUp;
        }

        public void Start()
        {
            m_Hook = new Thread(Subscribe) { IsBackground = true };
            m_Hook.Start();
        }

        public void SetInterval(double ms)
        {
            m_Timer.Interval = ms * 4;
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
            client.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_CURSORUP,
                args = new Dictionary<string, string>
                {
                    { "X", Convert.ToString(TouchPos.X) },
                    { "Y", Convert.ToString(TouchPos.Y) },
                    { "Button", Convert.ToString((int)TouchPos.Button) }
                }
            });
        }

        private void OnMouseDown(object sender, MouseEventExtArgs e)
        {
            m_Events.MouseMoveExt += OnMouseMove;

            m_Timer.Stop();
            client.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_CURSORDOWN,
                args = new Dictionary<string, string>
                {
                    { "X", Convert.ToString(e.X) },
                    { "Y", Convert.ToString(e.Y) },
                    { "Button", Convert.ToString((int)e.Button) }
                }
            });
        }

        private void OnMouseMove(object sender, MouseEventExtArgs e)
        {
            client.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_CURSORMOVE,
                args = new Dictionary<string, string>
                {
                    { "X", Convert.ToString(e.X) },
                    { "Y", Convert.ToString(e.Y) },
                    { "Button", Convert.ToString((int)e.Button) }
                }
            });
        }

        private void OnMouseUp(object sender, MouseEventExtArgs e)
        {
            m_Events.MouseMoveExt -= OnMouseMove;

            TouchPos = new TouchInput()
            {
                X = e.X,
                Y = e.Y,
                Button = e.Button
            };

            m_Timer.Start();
        }

        internal void Stop()
        {
            m_Events.MouseDownExt -= OnMouseDown;
            m_Events.MouseUpExt -= OnMouseUp;
            m_Events.Dispose();
            m_Events = null;
        }
    }
}
