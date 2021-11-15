using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerService
{
    [Serializable]
    public class PipeMessage
    {
        public PipeCode Code;
        public string[] args = new string[] { };
    }

    public enum PipeCode
    {
        CODE_HELLO = 0,
        CODE_PROCESS = 1,
        CODE_TOAST = 2,
        CODE_CURSOR_UP = 3,
        CODE_CURSOR_DOWN = 4,
        CODE_CURSOR_MOVE = 5
    }

    public class PipeServer
    {
        private readonly NamedPipeServer<PipeMessage> server;
        private readonly ILogger<ControllerService> logger;
        private readonly ControllerService service;

        private List<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeServer(string pipeName, ControllerService service, ILogger<ControllerService> logger)
        {
            this.service = service;
            this.logger = logger;

            m_queue = new List<PipeMessage>();

            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            PipeSecurity ps = new PipeSecurity();

            System.Security.Principal.SecurityIdentifier sid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule par = new PipeAccessRule(sid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);

            ps.AddAccessRule(par);

            server = new NamedPipeServer<PipeMessage>(pipeName, ps);
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
        }

        public void Start()
        {
            if (server == null)
                return;

            server.Start();

            logger.LogInformation($"Pipe Server has started.");
        }

        public void Stop()
        {
            if (server == null)
                return;

            server.Stop();
        }

        private void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            connected = true;
            logger.LogInformation("Client {0} is now connected!", connection.Id);
            connection.PushMessage(new PipeMessage { Code = PipeCode.CODE_HELLO });
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            connected = false;
            logger.LogInformation("Client {0} disconnected", connection.Id);
        }

        private void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            logger.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.Code, string.Join(" ", message.args));

            switch (message.Code)
            {
                case PipeCode.CODE_PROCESS:
                    service.UpdateProcess(int.Parse(message.args[0]));
                    break;
                case PipeCode.CODE_CURSOR_UP:
                    service.PhysicalController.touch.OnMouseUp();
                    break;
                case PipeCode.CODE_CURSOR_DOWN:
                    service.PhysicalController.touch.OnMouseDown();
                    break;
                case PipeCode.CODE_CURSOR_MOVE:
                    service.PhysicalController.touch.OnMouseMove(short.Parse(message.args[0]), short.Parse(message.args[1]));
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            logger.LogError("PipeServer failed. {exception.Message}");
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                m_queue.Add(message);
                m_timer.Start();
                return;
            }

            server.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                server.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
