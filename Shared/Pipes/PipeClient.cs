using HandheldCompanion.Shared;
using ricaun.NamedPipeWrapper;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Shared.Pipes
{
    public class PipeClient
    {
        public delegate void ConnectedEventHandler();

        public delegate void DisconnectedEventHandler();

        public delegate void ServerMessageEventHandler(PipeMessage e);

        private const string PipeName = "HandheldCompanion";
        public NamedPipeClient<PipeMessage> client;

        private readonly ConcurrentQueue<PipeMessage> m_queue = new();
        private readonly Timer m_timer;

        public bool IsConnected;

        public PipeClient()
        {
            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            client = new NamedPipeClient<PipeMessage>(PipeName);
            client.AutoReconnect = true;
        }

        public event ConnectedEventHandler Connected;
        public event DisconnectedEventHandler Disconnected;
        public event ServerMessageEventHandler ServerMessage;

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke();

            IsConnected = false;
        }

        public void Open()
        {
            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnServerMessage;
            client.Error += OnError;

            client?.Start();
            LogManager.LogInformation("{0} has started", "PipeClient");
        }

        public void Close()
        {
            client.Disconnected -= OnClientDisconnected;
            client.ServerMessage -= OnServerMessage;
            client.Error -= OnError;

            client?.Stop();
            LogManager.LogInformation("{0} has stopped", "PipeClient");
            client = null;
        }

        private void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            ServerMessage?.Invoke(message);

            switch (message.code)
            {
                case PipeCode.SERVER_PING:
                    IsConnected = true;
                    Connected?.Invoke();
                    LogManager.LogInformation("Client {0} is now connected!", connection.Id);
                    break;
            }
        }

        private void OnError(Exception exception)
        {
            LogManager.LogError("{0} failed. {1}", "PipeClient", exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!IsConnected)
            {
                Type nodeType = message.GetType();

                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            client?.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!IsConnected)
                return;

            foreach (var m in m_queue)
                client?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }

        public void ClearQueue()
        {
            m_queue.Clear();
        }
    }
}
