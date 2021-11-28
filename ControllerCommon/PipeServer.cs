using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using Serilog.Core;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon
{
    public enum PipeCode
    {
        SERVER_PING = 0,                    // Sent to client during initialization
                                            // args: ...

        CLIENT_PROFILE = 1,                 // Sent to server each time a new process is in the foreground. Used to switch profiles
                                            // args: process id, process path

        SERVER_TOAST = 2,                   // Sent to client to display toast notification.
                                            // args: title, message

        CLIENT_CURSOR = 3,                  // Sent to server when mouse click is up
                                            // args: cursor x, cursor Y

        SERVER_SETTINGS = 6,                // Sent to client during initialization
                                            // args: ...

        SERVER_CONTROLLER = 7,              // Sent to client during initialization
                                            // args: ...

        CLIENT_SETTINGS = 8,                // Sent to server to update settings
                                            // args: ...

        CLIENT_HIDDER = 9,                  // Sent to server to register applications
                                            // args: ...

        CLIENT_SCREEN = 11,                 // Sent to server to update screen details
                                            // args: width, height
    }

    public class PipeServer
    {
        private NamedPipeServer<PipeMessage> server;
        private readonly ILogger logger;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(Object sender);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(Object sender);

        public event ClientMessageEventHandler ClientMessage;
        public delegate void ClientMessageEventHandler(Object sender, PipeMessage e);

        private ConcurrentQueue<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeServer(string pipeName, ILogger logger)
        {
            this.logger = logger;

            m_queue = new ConcurrentQueue<PipeMessage>();

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
            logger.LogInformation($"Pipe Server has started");
        }

        public void Stop()
        {
            if (server == null)
                return;

            server = null;
            logger.LogInformation($"Pipe Server has halted");
        }

        private void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            logger.LogInformation("Client {0} is now connected!", connection.Id);
            Connected?.Invoke(this);

            connected = true;

            // send ping
            SendMessage(new PipeServerPing());
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            logger.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke(this);

            connected = false;
        }

        private void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            logger.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.code, string.Join(" ", message.ToString()));
            ClientMessage?.Invoke(this, message);
        }

        private void OnError(Exception exception)
        {
            logger.LogError("PipeServer failed. {0}", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                m_queue.Enqueue(message);
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
