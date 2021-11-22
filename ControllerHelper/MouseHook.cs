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
        private IKeyboardMouseEvents m_Events;
        private Thread m_Hook;
        private Timer m_Timer;

        private PipeClient client;
        private Vector2 TouchPos;

        public MouseHook(PipeClient client)
        {
            this.client = client;

            // send MouseUp after 10ms interval
            m_Timer = new Timer() { Enabled = false, Interval = 10, AutoReset = false };
            m_Timer.Elapsed += SendMouseUp;
        }

        public void Start()
        {
            m_Hook = new Thread(Subscribe) { IsBackground = true };
            m_Hook.Start();
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
                    { "Y", Convert.ToString(TouchPos.Y) }
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
                    { "Y", Convert.ToString(e.Y) }
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
                    { "Y", Convert.ToString(e.Y) }
                }
            });
        }

        private void OnMouseUp(object sender, MouseEventExtArgs e)
        {
            m_Events.MouseMoveExt -= OnMouseMove;

            TouchPos = new Vector2(e.X, e.Y);
            m_Timer.Start();
        }

        internal void Stop()
        {
            m_Events.Dispose();
        }
    }
}
