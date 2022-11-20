using ControllerCommon.Managers;
using NamedPipeWrapper;
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

        CLIENT_INPUT = 7,                   // Sent to server to request a specific gamepad input
                                            // args: ...

        CLIENT_SETTINGS = 8,                // Sent to server to update settings
                                            // args: ...

        OBSOLETE_0 = 9,                     // OBSOLETE, REUSEME

        OBSOLETE_1 = 11,                    // OBSOLETE, REUSEME

        CLIENT_CONSOLE = 12,                // Sent from client to client to pass parameters
                                            // args: string[] parameters

        FORCE_SHUTDOWN = 13,                // Sent to server or client to halt process
                                            // args: ...

        SERVER_SENSOR = 14,                 // Sent to client to share sensor values
                                            // args: ...

        CLIENT_NAVIGATED = 15,              // Sent to server to share current navigated page
                                            // args: ...

        CLIENT_OVERLAY = 16,                // Sent to server to share current overlay status
                                            // args: ...

        SERVER_VIBRATION = 17,              // Sent to client to notify a vibration feedback arrived
                                            // args: ...

        OBSOLETE_2 = 18,                    // OBSOLETE, REUSEME

        CLIENT_CLEARINDEX = 19,             // Sent to server to clear all hidden controllers
                                            // args: ...
    }

    public static class PipeServer
    {
        private static NamedPipeServer<PipeMessage> server;

        public static event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler();

        public static event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler();

        public static event ClientMessageEventHandler ClientMessage;
        public delegate void ClientMessageEventHandler(PipeMessage e);

        private static ConcurrentQueue<PipeMessage> m_queue = new();
        private static Timer m_timer;

        public static bool IsConnected;

        public static void Initialize(string pipeName)
        {
            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            PipeSecurity ps = new();
            System.Security.Principal.SecurityIdentifier sid = new(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule par = new(sid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            ps.AddAccessRule(par);

            server = new NamedPipeServer<PipeMessage>(pipeName, ps);
        }

        public static void Open()
        {
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
            server?.Start();

            LogManager.LogInformation("{0} has started", "PipeServer");
        }

        public static void Close()
        {
            server.ClientConnected -= OnClientConnected;
            server.ClientDisconnected -= OnClientDisconnected;
            server.ClientMessage -= OnClientMessage;
            server.Error -= OnError;
            server?.Stop();

            LogManager.LogInformation("{0} has stopped", "PipeServer");
            server = null;
        }

        private static void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} is now connected!", connection.Id);
            Connected?.Invoke();

            IsConnected = true;

            // send ping
            SendMessage(new PipeServerPing());
        }

        private static void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke();

            IsConnected = false;
        }

        private static void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            ClientMessage?.Invoke(message);
        }

        private static void OnError(Exception exception)
        {
            LogManager.LogError("{0} failed. {1}", "PipeServer", exception.Message);
        }

        public static void SendMessage(PipeMessage message)
        {
            if (!IsConnected)
            {
                Type nodeType = message.GetType();
                if (nodeType == typeof(PipeSensor))
                    return;

                m_queue.Enqueue(message);
                m_timer.Start();
                return;
            }

            server?.PushMessage(message);
        }

        private static void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!IsConnected)
                return;

            foreach (PipeMessage m in m_queue)
                server?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
