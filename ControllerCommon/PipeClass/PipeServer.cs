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

        OBSOLETE_0 = 7,                     // OBSOLETE, REUSEME

        CLIENT_SETTINGS = 8,                // Sent to server to update settings
                                            // args: ...

        CLIENT_HIDDER = 9,                  // Sent to server to register applications
                                            // args: ...

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

        OBSOLETE_2 = 17,                    // OBSOLETE, REUSEME

        CLIENT_CONTROLLERINDEX = 18,        // Sent to server to share details on controller
                                            // args: ...

        CLIENT_CLEARINDEX = 19,             // Sent to server to clear all hidden controllers
                                            // args: ...
    }

    public class PipeServer
    {
        private NamedPipeServer<PipeMessage> server;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(Object sender);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(Object sender);

        public event ClientMessageEventHandler ClientMessage;
        public delegate void ClientMessageEventHandler(Object sender, PipeMessage e);

        private readonly ConcurrentQueue<PipeMessage> m_queue;
        private readonly Timer m_timer;

        public bool connected;

        public PipeServer(string pipeName)
        {
            m_queue = new ConcurrentQueue<PipeMessage>();

            // monitors processes and settings
            m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
            m_timer.Elapsed += SendMessageQueue;

            PipeSecurity ps = new();
            System.Security.Principal.SecurityIdentifier sid = new(System.Security.Principal.WellKnownSidType.BuiltinUsersSid, null);
            PipeAccessRule par = new(sid, PipeAccessRights.ReadWrite, System.Security.AccessControl.AccessControlType.Allow);
            ps.AddAccessRule(par);

            server = new NamedPipeServer<PipeMessage>(pipeName, ps);
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public void Open()
        {
            server?.Start();
            LogManager.LogInformation("{0} has started", this.ToString());
        }

        public void Close()
        {
            server?.Stop();
            LogManager.LogInformation("{0} has stopped", this.ToString());
            server = null;
        }

        private void OnClientConnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} is now connected!", connection.Id);
            Connected?.Invoke(this);

            connected = true;

            // send ping
            SendMessage(new PipeServerPing());
        }

        private void OnClientDisconnected(NamedPipeConnection<PipeMessage, PipeMessage> connection)
        {
            LogManager.LogInformation("Client {0} disconnected", connection.Id);
            Disconnected?.Invoke(this);

            connected = false;
        }

        private void OnClientMessage(NamedPipeConnection<PipeMessage, PipeMessage> connection, PipeMessage message)
        {
            LogManager.LogDebug("Client {0} opcode: {1} says: {2}", connection.Id, message.code, string.Join(" ", message.ToString()));
            ClientMessage?.Invoke(this, message);
        }

        private void OnError(Exception exception)
        {
            LogManager.LogError("{0} failed. {1}", this.ToString(), exception.Message);
        }

        public void SendMessage(PipeMessage message)
        {
            if (!connected)
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

        private void SendMessageQueue(object sender, ElapsedEventArgs e)
        {
            if (!connected)
                return;

            foreach (PipeMessage m in m_queue)
                server?.PushMessage(m);

            m_queue.Clear();
            m_timer.Stop();
        }
    }
}
