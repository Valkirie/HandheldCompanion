using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon
{
    public class PipeClient
    {
        public NamedPipeClient<PipeMessage> client;
        private readonly ILogger logger;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(Object sender);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(Object sender);

        public event ServerMessageEventHandler ServerMessage;
        public delegate void ServerMessageEventHandler(Object sender, PipeMessage e);

        private ConcurrentQueue<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeClient(string pipeName)
        {
            m_queue = new ConcurrentQueue<PipeMessage>();

            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            client = new NamedPipeClient<PipeMessage>(pipeName);
            client.AutoReconnect = true;

            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnServerMessage;
            client.Error += OnError;
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public PipeClient(string pipeName, ILogger logger) : this(pipeName)
        {
            this.logger = logger;
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            logger?.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke(this);

            connected = false;
        }

        public void Open()
        {
            client?.Start();
            logger?.LogInformation("{0} has started", this.ToString());
        }

        public void Close()
        {
            client?.Stop();
            logger?.LogInformation("{0} has stopped", this.ToString());
            client = null;
        }

        private void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            logger?.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.code, string.Join(" ", message.ToString()));
            ServerMessage?.Invoke(this, message);

            switch (message.code)
            {
                case PipeCode.SERVER_PING:
                    connected = true;
                    Connected?.Invoke(this);
                    logger?.LogInformation("Client {0} is now connected!", connection.Id);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            logger?.LogError("{0} failed. {1}", this.ToString(), exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                Type nodeType = message.GetType();
                if (nodeType == typeof(PipeClientCursor))
                    return;

                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            client?.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                client?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
