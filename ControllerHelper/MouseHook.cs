using ControllerService;
using Gma.System.MouseKeyHook;
using System;
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

        public MouseHook(PipeClient client)
        {
            this.client = client;

            // send MouseUp after 50ms interval
            m_Timer = new Timer() { Enabled = false, Interval = 50, AutoReset = false };
            m_Timer.Elapsed += SendMouseUp;

            m_Hook = new Thread(Subscribe) { IsBackground = true };
        }

        public void Start()
        {
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

        private void SendMouseUp(object sender, ElapsedEventArgs e)
        {
            client.SendMessage(new PipeMessage { Code = PipeCode.CODE_CURSOR_UP });
        }

        private void OnMouseDown(object sender, MouseEventExtArgs e)
        {
            m_Timer.Stop();
            client.SendMessage(new PipeMessage { Code = PipeCode.CODE_CURSOR_DOWN });
        }

        private void OnMouseMove(object sender, MouseEventExtArgs e)
        {
            client.SendMessage(new PipeMessage { Code = PipeCode.CODE_CURSOR_MOVE, args = new string[] { Convert.ToString(e.X), Convert.ToString(e.Y) } });
        }

        private void OnMouseUp(object sender, MouseEventExtArgs e)
        {
            m_Timer.Start();
        }

        internal void Stop()
        {
            m_Events.Dispose();
            m_Hook = null;
        }
    }
}
