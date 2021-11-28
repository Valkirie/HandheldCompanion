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
        public readonly NamedPipeClient<PipeMessage> client;
        private readonly ILogger logger;

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(Object sender);

        public event ServerMessageEventHandler ServerMessage;
        public delegate void ServerMessageEventHandler(Object sender, PipeMessage e);

        private ConcurrentQueue<PipeMessage> m_queue;
        private Timer m_timer;

        public bool connected;

        public PipeClient(string pipeName, ILogger logger)
        {
            this.logger = logger;

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

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            logger.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke(this);

            connected = false;
        }

        public void Start()
        {
            if (client == null)
                return;

            client.Start();
            logger.LogInformation($"Pipe Client has started");
        }

        public void Stop()
        {
            if (client == null)
                return;

            client.Stop();
            logger.LogInformation($"Pipe Client has stopped");
        }

        private void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            logger.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.code, string.Join(" ", message.ToString()));
            ServerMessage?.Invoke(this, message);

            switch (message.code)
            {
                case PipeCode.SERVER_PING:
                    connected = true;
                    logger.LogInformation("Client {0} is now connected!", connection.Id);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            logger.LogError("PipClient failed. {0}", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
            {
                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            client.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                client.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
