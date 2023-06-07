using System;
using System.Collections.Concurrent;
using System.Timers;
using ControllerCommon.Managers;
using NamedPipeWrapper;

namespace ControllerCommon.Pipes;

public static class PipeClient
{
    public delegate void ConnectedEventHandler();

    public delegate void DisconnectedEventHandler();

    public delegate void ServerMessageEventHandler(PipeMessage e);

    private const string PipeName = "HandheldCompanion";
    public static NamedPipeClient<PipeMessage> client;

    private static readonly ConcurrentQueue<PipeMessage> m_queue = new();
    private static readonly Timer m_timer;

    public static bool IsConnected;

    static PipeClient()
    {
        // monitors processes and settings
        m_timer = new Timer(1000) { Enabled = false, AutoReset = true };
        m_timer.Elapsed += SendMessageQueue;

        client = new NamedPipeClient<PipeMessage>(PipeName);
        client.AutoReconnect = true;
    }

    public static event ConnectedEventHandler Connected;

    public static event DisconnectedEventHandler Disconnected;

    public static event ServerMessageEventHandler ServerMessage;

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
            var nodeType = message.GetType();
            if (nodeType == typeof(PipeClientCursor))
                return;
            if (nodeType == typeof(PipeClientInputs))
                return;
            if (nodeType == typeof(PipeClientMovements))
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

        foreach (var m in m_queue)
            client?.PushMessage(m);

        m_queue.Clear();
        m_timer.Stop();
    }

    public static void ClearQueue()
    {
        m_queue.Clear();
    }
}