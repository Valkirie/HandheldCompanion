using ControllerCommon.Managers;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ControllerCommon
{
    public static class PipeClient
    {
        public static NamedPipeClient<PipeMessage> client;

        public static event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler();

        public static event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler();

        public static event ServerMessageEventHandler ServerMessage;
        public delegate void ServerMessageEventHandler(PipeMessage e);

        private static ConcurrentQueue<PipeMessage> m_queue = new();
        private static Timer m_timer;

        public static bool IsConnected;

        public static void Initialize(string pipeName)
        {
            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            client = new NamedPipeClient<PipeMessage>(pipeName);
            client.AutoReconnect = true;
        }

        private static void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke();

            IsConnected = false;
        }

        public static void Open()
        {
            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnServerMessage;
            client.Error += OnError;

            client?.Start();
            LogManager.LogInformation("{0} has started", "PipeClient");
        }

        public static void Close()
        {
            client.Disconnected -= OnClientDisconnected;
            client.ServerMessage -= OnServerMessage;
            client.Error -= OnError;

            client?.Stop();
            LogManager.LogInformation("{0} has stopped", "PipeClient");
            client = null;
        }

        private static void OnServerMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
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

        private static void OnError(Exception exception)
        {
            LogManager.LogError("{0} failed. {1}", "PipeClient", exception.Message);
        }

        public static void SendMessage(PipeMessage message)
        {
            if (!IsConnected)
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

        private static void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!IsConnected)
                return;

            foreach (PipeMessage m in m_queue)
                client?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
