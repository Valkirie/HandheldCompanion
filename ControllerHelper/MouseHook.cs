using ControllerService;
using Gma.System.MouseKeyHook;
using Serilog.Core;
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

        private readonly PipeClient client;
        private readonly ControllerHelper helper;
        private readonly Logger logger;

        private TouchInput TouchPos;

        public MouseHook(PipeClient client, ControllerHelper helper, Logger logger)
        {
            this.client = client;
            this.helper = helper;
            this.logger = logger;

            // send MouseUp after default interval (40ms)
            m_Timer = new Timer() { Enabled = false, Interval = 40, AutoReset = false };
            m_Timer.Elapsed += SendMouseUp;
        }

        public void Start()
        {
            if (m_Hook != null)
                return;

            m_Hook = new Thread(Subscribe) { IsBackground = true };
            m_Hook.Start();

            logger.Information("Mouse hook has started");
        }

        public void SetInterval(double ms)
        {
            m_Timer.Interval = ms * 4;
            logger.Information("Mouse hook interval set to {0}", m_Timer.Interval);
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
            logger.Verbose("OnMouseDown x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);
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
            logger.Verbose("OnMouseMove x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);
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
            logger.Verbose("OnMouseUp x:{0} y:{1} button:{2}", e.X, e.Y, e.Button);

            m_Timer.Start();
        }

        internal void Stop()
        {
            m_Events.MouseDownExt -= OnMouseDown;
            m_Events.MouseUpExt -= OnMouseUp;
            m_Events.Dispose();
            m_Events = null;

            logger.Information("Mouse hook has halted");
        }
    }
}
