using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Timers;
using ControllerCommon.Managers;
using NamedPipeWrapper;

namespace ControllerCommon.Pipes;

public static class PipeServer
{
    public delegate void ClientMessageEventHandler(PipeMessage e);

    public delegate void ConnectedEventHandler();

    public delegate void DisconnectedEventHandler();

    private const string PipeName = "HandheldCompanion";
    private static NamedPipeServer<PipeMessage> server;

    private static readonly ConcurrentQueue<PipeMessage> m_queue = new();
    private static readonly Timer m_timer;

    public static bool IsConnected;

    static PipeServer()
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

    public static event ConnectedEventHandler Connected;

    public static event DisconnectedEventHandler Disconnected;

    public static event ClientMessageEventHandler ClientMessage;

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
            var nodeType = message.GetType();
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

        foreach (var m in m_queue)
            server?.PushMessage(m);

        m_queue.Clear();
        m_timer.Stop();
    }

    public static void ClearQueue()
    {
        m_queue.Clear();
    }
}