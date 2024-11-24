using HandheldCompanion.Shared;
using ricaun.NamedPipeWrapper;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Timers;
using static Shared.Pipes.PipeMessage;
using Timer = System.Timers.Timer;

namespace Shared.Pipes
{
    public class PipeServer
    {
        public delegate void ClientMessageEventHandler(PipeMessage e);

        public delegate void ConnectedEventHandler();

        public delegate void DisconnectedEventHandler();

        private const string PipeName = "HandheldCompanion";
        private NamedPipeServer<PipeMessage> server;

        private readonly ConcurrentQueue<PipeMessage> m_queue = new();
        private readonly Timer m_timer;

        public bool IsConnected;

        PipeServer()
        {
            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            PipeSecurity ps = new();
            SecurityIdentifier sid = new(WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule par = new(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            ps.AddAccessRule(par);

            server = new NamedPipeServer<PipeMessage>(PipeName, ps);
        }

        public event ConnectedEventHandler Connected;

        public event DisconnectedEventHandler Disconnected;

        public event ClientMessageEventHandler ClientMessage;

        public void Open()
        {
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
            server?.Start();

            LogManager.LogInformation("{0} has started", "PipeServer");
        }

        public void Close()
        {
            server.ClientConnected -= OnClientConnected;
            server.ClientDisconnected -= OnClientDisconnected;
            server.ClientMessage -= OnClientMessage;
            server.Error -= OnError;
            server?.Stop();

            LogManager.LogInformation("{0} has stopped", "PipeServer");
            server = null;
        }

        private void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} is now connected!", connection.Id);
            Connected?.Invoke();

            IsConnected = true;

            // send ping
            SendMessage(new PipeServerPing());
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke();

            IsConnected = false;
        }

        private void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            ClientMessage?.Invoke(message);
        }

        private void OnError(Exception exception)
        {
            LogManager.LogError("{0} failed. {1}", "PipeServer", exception.Message);
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

            server?.PushMessage(message);
        }

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!IsConnected)
                return;

            foreach (var m in m_queue)
                server?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }

        public void ClearQueue()
        {
            m_queue.Clear();
        }
    }
}
