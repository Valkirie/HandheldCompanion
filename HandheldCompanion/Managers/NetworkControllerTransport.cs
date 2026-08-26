using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using HandheldCompanion.Shared;

namespace HandheldCompanion.Managers;

internal enum NetworkControllerOpCode : byte
{
    Discovery = 1,
    RequestController = 2,
    ControllerAdvertisement = 3,
    ControllerState = 4,
    Vibration = 5,
    Metadata = 6,
    SessionClosed = 7
}

internal sealed record NetworkControllerMetadata(ControllerCapabilities Capabilities, ButtonFlags[] SourceButtons, AxisLayoutFlags[] SourceAxis, Dictionary<ButtonFlags, string> ButtonGlyphs, Dictionary<AxisFlags, string> AxisGlyphs, Dictionary<AxisLayoutFlags, string> LayoutGlyphs, Dictionary<ButtonFlags, string> FontFamilies);
internal sealed record NetworkControllerPacket(NetworkControllerOpCode OpCode, Guid Id, string Name, byte UserIndex, uint Sequence, ControllerState State, IPEndPoint Endpoint, NetworkControllerMetadata? Metadata);

internal sealed record NetworkControllerAuthorization(IPEndPoint Endpoint, long LastRequestTicks);

internal static class NetworkControllerTransport
{
    private const int Port = 26780;
    private const int NameLength = 64;
    private const int ButtonBytes = ((int)ButtonFlags.Max + 7) / 8;
    private const int AxisBytes = ((int)AxisFlags.Max - 1) * sizeof(short);
    private const int PacketLength = 1 + 16 + 1 + NameLength + 1 + 4 + ButtonBytes + AxisBytes + (6 * sizeof(float));
    private const int RequestLength = 1 + 16;
    private const int SessionClosedLength = 1 + 16;
    private const int MetadataHeaderLength = 1 + 16 + sizeof(int);
    private const int DiscoveryLength = 1;
    private const int AuthorizationTimeoutMilliseconds = 3000;
    private static readonly Guid InstanceId = Guid.NewGuid();
    private static readonly ConcurrentDictionary<Guid, uint> Sequences = new();
    private static readonly ConcurrentDictionary<Guid, byte> PublishedControllers = new();
    private static readonly ConcurrentDictionary<IPAddress, byte> DiscoveryPeers = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, NetworkControllerAuthorization>> AuthorizedPeers = new();
    private static UdpClient? sender;
    private static UdpClient? receiver;
    private static CancellationTokenSource? cancellation;
    private static Task? receiveTask;
    private static Task? discoveryTask;
    private static bool running;
    private static int lastPhysicalControllerCount = -1;

    public static event Action<NetworkControllerPacket>? PacketReceived;
    public static event Action<Guid, byte, byte>? VibrationReceived;
    public static event Action? AuthorizationChanged;
    public static event Action<Guid, bool>? StreamingChanged;

    public static bool IsBroadcasting(IController controller)
    {
        if (controller is RemoteController)
            return false;

        return IsBroadcasting(GetNetworkControllerId(controller.GetInstanceId()));
    }

    private static NetworkControllerMetadata CreateMetadata(IController controller)
    {
        Dictionary<ButtonFlags, string> buttonGlyphs = [];
        Dictionary<AxisFlags, string> axisGlyphs = [];
        Dictionary<AxisLayoutFlags, string> layoutGlyphs = [];
        Dictionary<ButtonFlags, string> fontFamilies = [];
        foreach (ButtonFlags button in controller.GetSourceButtons())
        {
            buttonGlyphs[button] = controller.GetGlyph(button);
            fontFamilies[button] = controller.GetFontFamily(button);
        }
        foreach (AxisLayoutFlags axis in controller.GetSourceAxis())
            layoutGlyphs[axis] = controller.GetGlyph(axis);
        foreach (AxisFlags axis in Enum.GetValues<AxisFlags>())
            axisGlyphs[axis] = controller.GetGlyph(axis);
        return new(controller.Capabilities, [.. controller.GetSourceButtons()], [.. controller.GetSourceAxis()], buttonGlyphs, axisGlyphs, layoutGlyphs, fontFamilies);
    }

    public static bool IsBroadcasting(Guid id)
    {
        if (!AuthorizedPeers.TryGetValue(id, out ConcurrentDictionary<string, NetworkControllerAuthorization>? peers))
        {
            EndPublishing(id);
            return false;
        }

        long now = Environment.TickCount64;
        bool removed = false;
        foreach (KeyValuePair<string, NetworkControllerAuthorization> peer in peers.ToArray())
        {
            if (now - peer.Value.LastRequestTicks > AuthorizationTimeoutMilliseconds && peers.TryRemove(peer.Key, out _))
                removed = true;
        }

        if (removed)
            AuthorizationChanged?.Invoke();

        bool broadcasting = peers.Values.Any(peer => now - peer.LastRequestTicks <= AuthorizationTimeoutMilliseconds);
        if (!broadcasting)
            EndPublishing(id);

        return broadcasting;
    }

    private static void EndPublishing(Guid id)
    {
        if (PublishedControllers.TryRemove(id, out _))
            StreamingChanged?.Invoke(id, false);
    }

    public static string? GetBroadcastingPeer(IController controller)
    {
        if (controller is RemoteController)
            return null;

        Guid id = GetNetworkControllerId(controller.GetInstanceId());
        if (!AuthorizedPeers.TryGetValue(id, out ConcurrentDictionary<string, NetworkControllerAuthorization>? peers))
            return null;

        long now = Environment.TickCount64;
        NetworkControllerAuthorization? peer = peers.Values.FirstOrDefault(item => now - item.LastRequestTicks <= AuthorizationTimeoutMilliseconds);
        return peer?.Endpoint.Address.ToString();
    }

    public static void Start()
    {
        if (running)
            return;

        running = true;
        cancellation = new CancellationTokenSource();
        lastPhysicalControllerCount = -1;
        sender = new UdpClient { EnableBroadcast = true };
        receiver = new UdpClient(Port) { EnableBroadcast = true };
        receiveTask = ReceiveLoop(cancellation.Token);
        discoveryTask = DiscoveryLoop(cancellation.Token);
        LogManager.LogInformation("Network controller transport started on UDP port {0}", Port);
    }

    public static void Stop()
    {
        if (!running)
            return;

        foreach (Guid id in PublishedControllers.Keys)
        {
            SendSessionClosed(id);
            EndPublishing(id);
        }

        running = false;
        cancellation?.Cancel();
        sender?.Dispose();
        receiver?.Dispose();
        sender = null;
        receiver = null;
        cancellation?.Dispose();
        cancellation = null;
        PublishedControllers.Clear();
        DiscoveryPeers.Clear();
        AuthorizedPeers.Clear();
        AuthorizationChanged?.Invoke();
        LogManager.LogInformation("Network controller transport stopped");
    }

    public static void SendSessionClosed(Guid id)
    {
        if (sender is null)
            return;

        byte[] packet = new byte[SessionClosedLength];
        packet[0] = (byte)NetworkControllerOpCode.SessionClosed;
        id.TryWriteBytes(packet.AsSpan(1, 16));
        try { sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    public static void Publish(IController controller)
    {
        if (!running)
            return;

        if (controller is RemoteController || !controller.IsPhysical() || !controller.IsConnected())
            return;

        string instance = controller.GetInstanceId();
        Guid id = GetNetworkControllerId(instance);
        ConcurrentDictionary<string, NetworkControllerAuthorization>? peers = AuthorizedPeers.TryGetValue(id, out ConcurrentDictionary<string, NetworkControllerAuthorization>? authorized) ? authorized : null;
        if (peers is null || peers.IsEmpty)
            return;

        if (!IsBroadcasting(id))
            return;

        uint sequence = Sequences.AddOrUpdate(id, 1, (_, value) => value + 1);
        byte[] packet = Encode(id, GetNetworkControllerName(controller), (byte)controller.GetUserIndex(), sequence, controller.Inputs, false);
        try
        {
            if (sender is null)
            {
                LogManager.LogWarning("Network controller state was not sent because the UDP sender is unavailable");
                return;
            }

            long now = Environment.TickCount64;
            foreach (var peer in peers.ToArray())
            {
                if (now - peer.Value.LastRequestTicks > AuthorizationTimeoutMilliseconds)
                    continue;

                sender.Send(packet, packet.Length, peer.Value.Endpoint);
            }
            if (PublishedControllers.TryAdd(id, 0))
            {
                LogManager.LogInformation("Network controller state broadcast started for {0} ({1})", controller, id);
                StreamingChanged?.Invoke(id, true);
            }
        }
        catch (SocketException ex) { LogManager.LogWarning("Network controller state broadcast failed: {0}", ex.Message); }
        catch (ObjectDisposedException) { }
    }

    private static async Task ReceiveLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await receiver!.ReceiveAsync(token).ConfigureAwait(false);
                if (result.Buffer.Length == DiscoveryLength && result.Buffer[0] == (byte)NetworkControllerOpCode.Discovery)
                {
                    if (DiscoveryPeers.TryAdd(result.RemoteEndPoint.Address, 0))
                        LogManager.LogInformation("Network controller discovery request received from {0}", result.RemoteEndPoint.Address);

                    IController[] controllers = ControllerManager.GetPhysicalControllers<IController>()
                        .Where(controller => controller is not RemoteController)
                        .ToArray();
                    if (controllers.Length != lastPhysicalControllerCount)
                    {
                        lastPhysicalControllerCount = controllers.Length;
                        LogManager.LogInformation("Responding to network controller discovery with {0} physical controller(s)", controllers.Length);
                    }

                    foreach (IController controller in controllers)
                        SendAdvertisement(controller, CreatePeerEndpoint(result.RemoteEndPoint));
                    continue;
                }

                if (TryDecodeVibration(result.Buffer, out Guid vibrationId, out byte largeMotor, out byte smallMotor))
                {
                    VibrationReceived?.Invoke(vibrationId, largeMotor, smallMotor);
                    continue;
                }

                if (TryDecodeRequest(result.Buffer, out Guid requestedId))
                {
                    ConcurrentDictionary<string, NetworkControllerAuthorization> peers = AuthorizedPeers.GetOrAdd(requestedId, _ => new());
                    IPEndPoint peerEndpoint = CreatePeerEndpoint(result.RemoteEndPoint);
                    peers[peerEndpoint.ToString()] = new(peerEndpoint, Environment.TickCount64);
                    AuthorizationChanged?.Invoke();
                    continue;
                }

                if (TryDecodeSessionClosed(result.Buffer, out Guid closedId))
                {
                    ControllerManager.RemoteControllerSessionClosed(closedId);
                    continue;
                }

                if (!TryDecode(result.Buffer, result.RemoteEndPoint, out NetworkControllerPacket? packet))
                {
                    LogManager.LogDebug("Invalid network controller packet received from {0} ({1} bytes)", result.RemoteEndPoint.Address, result.Buffer.Length);
                    continue;
                }

                if (PublishedControllers.ContainsKey(packet.Id))
                {
                    continue;
                }

                if (ControllerManager.GetPhysicalControllers<IController>()
                    .Where(controller => controller is not RemoteController)
                    .Any(controller => GetNetworkControllerId(controller.GetInstanceId()) == packet.Id))
                {
                    continue;
                }

                PacketReceived?.Invoke(packet);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            catch { }
        }
    }

    public static void RequestController(Guid id)
    {
        if (!running || sender is null)
            return;

        byte[] request = new byte[RequestLength];
        request[0] = (byte)NetworkControllerOpCode.RequestController;
        id.TryWriteBytes(request.AsSpan(1, 16));
        try { sender.Send(request, request.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    private static IPEndPoint CreatePeerEndpoint(IPEndPoint endpoint) => new(endpoint.Address, Port);

    private static async Task DiscoveryLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SendDiscovery();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private static void SendDiscovery()
    {
        try
        {
            byte[] discovery = { (byte)NetworkControllerOpCode.Discovery };
            sender?.Send(discovery, discovery.Length, new IPEndPoint(IPAddress.Broadcast, Port));
            LogManager.LogTrace("Network controller discovery broadcast sent");
        }
        catch (SocketException ex)
        {
            LogManager.LogWarning("Network controller discovery broadcast failed: {0}", ex.Message);
        }
        catch (ObjectDisposedException) { }
    }

    public static void SendVibration(Guid id, byte largeMotor, byte smallMotor)
    {
        if (!running || sender is null)
            return;

        byte[] packet = new byte[19];
        packet[0] = (byte)NetworkControllerOpCode.Vibration;
        id.TryWriteBytes(packet.AsSpan(1, 16));
        packet[17] = largeMotor;
        packet[18] = smallMotor;

        try { sender.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    private static bool TryDecodeVibration(byte[] packet, out Guid id, out byte largeMotor, out byte smallMotor)
    {
        id = Guid.Empty;
        largeMotor = 0;
        smallMotor = 0;
        if (packet.Length != 19 || packet[0] != (byte)NetworkControllerOpCode.Vibration)
            return false;

        id = new Guid(packet.AsSpan(1, 16));
        largeMotor = packet[17];
        smallMotor = packet[18];
        return true;
    }

    internal static Guid GetNetworkControllerId(string instance)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{InstanceId:N}:{instance}");
        Span<byte> hash = stackalloc byte[16];
        System.Security.Cryptography.MD5.HashData(bytes, hash);
        return new Guid(hash);
    }

    private static void SendAdvertisement(IController controller, IPEndPoint endpoint)
    {
        if (sender is null || !controller.IsPhysical() || !controller.IsConnected())
            return;

        Guid id = GetNetworkControllerId(controller.GetInstanceId());
        byte[] packet = Encode(id, GetNetworkControllerName(controller), (byte)controller.GetUserIndex(), 0, new ControllerState(), true);
        byte[] metadataPacket = EncodeMetadata(id, CreateMetadata(controller));
        try
        {
            sender.Send(packet, packet.Length, endpoint);
            sender.Send(metadataPacket, metadataPacket.Length, endpoint);
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    private static string GetNetworkControllerName(IController controller)
    {
        return $"{controller} ({IDevice.GetCurrent().GetType().Name})";
    }

    private static byte[] Encode(Guid id, string name, byte userIndex, uint sequence, ControllerState state, bool advertisement)
    {
        byte[] packet = new byte[PacketLength];
        packet[0] = (byte)(advertisement ? NetworkControllerOpCode.ControllerAdvertisement : NetworkControllerOpCode.ControllerState);
        _ = id.TryWriteBytes(packet.AsSpan(1, 16));
        string safeName = name ?? "Network Controller";
        byte[] nameBytes = Encoding.UTF8.GetBytes(safeName);
        int nameCount = Math.Min(nameBytes.Length, NameLength);
        packet[17] = (byte)nameCount;
        nameBytes.AsSpan(0, nameCount).CopyTo(packet.AsSpan(18, NameLength));
        packet[82] = userIndex;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(83, 4), sequence);

        int offset = 87;
        for (int i = 0; i < (int)ButtonFlags.Max; i++)
            if (state.ButtonState[(ButtonFlags)i]) packet[offset + i / 8] |= (byte)(1 << (i % 8));
        offset += ButtonBytes;
        for (int i = 1; i < (int)AxisFlags.Max; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(offset, 2), state.AxisState[(AxisFlags)i]);
            offset += 2;
        }

        Vector3 gyro = state.GyroState.GetGyroscope(GyroState.SensorState.Default);
        Vector3 accel = state.GyroState.GetAccelerometer(GyroState.SensorState.Default);
        foreach (float value in new[] { gyro.X, gyro.Y, gyro.Z, accel.X, accel.Y, accel.Z })
        {
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(offset, 4), BitConverter.SingleToInt32Bits(value));
            offset += 4;
        }
        return packet;
    }

    private static byte[] EncodeMetadata(Guid id, NetworkControllerMetadata metadata)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(metadata);
        byte[] packet = new byte[MetadataHeaderLength + payload.Length];
        packet[0] = (byte)NetworkControllerOpCode.Metadata;
        id.TryWriteBytes(packet.AsSpan(1, 16));
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(17, sizeof(int)), payload.Length);
        payload.CopyTo(packet.AsSpan(MetadataHeaderLength));
        return packet;
    }

    private static bool TryDecodeRequest(byte[] packet, out Guid id)
    {
        id = Guid.Empty;
        if (packet.Length != RequestLength || packet[0] != (byte)NetworkControllerOpCode.RequestController)
            return false;

        id = new Guid(packet.AsSpan(1, 16));
        return true;
    }

    private static bool TryDecodeSessionClosed(byte[] packet, out Guid id)
    {
        id = Guid.Empty;
        if (packet.Length != SessionClosedLength || packet[0] != (byte)NetworkControllerOpCode.SessionClosed)
            return false;

        id = new Guid(packet.AsSpan(1, 16));
        return true;
    }

    private static bool TryDecode(byte[] packet, IPEndPoint endpoint, out NetworkControllerPacket? result)
    {
        result = null;
        if (packet.Length >= MetadataHeaderLength && packet[0] == (byte)NetworkControllerOpCode.Metadata)
        {
            int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(17, sizeof(int)));
            if (metadataLength < 0 || packet.Length != MetadataHeaderLength + metadataLength)
                return false;

            Guid metadataId = new(packet.AsSpan(1, 16));
            try
            {
                NetworkControllerMetadata? metadata = JsonSerializer.Deserialize<NetworkControllerMetadata>(packet.AsSpan(MetadataHeaderLength, metadataLength));
                if (metadata is null)
                    return false;

                result = new(NetworkControllerOpCode.Metadata, metadataId, string.Empty, 0, 0, new ControllerState(), endpoint, metadata);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (packet.Length != PacketLength || (packet[0] != (byte)NetworkControllerOpCode.ControllerState && packet[0] != (byte)NetworkControllerOpCode.ControllerAdvertisement))
            return false;

        NetworkControllerOpCode opCode = (NetworkControllerOpCode)packet[0];
        Guid id = new(packet.AsSpan(1, 16));
        int nameCount = Math.Min((int)packet[17], NameLength);
        string name = Encoding.UTF8.GetString(packet, 18, nameCount);
        byte userIndex = packet[82];
        uint sequence = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(83, 4));
        ControllerState state = new();
        int offset = 87;
        for (int i = 0; i < (int)ButtonFlags.Max; i++)
            state.ButtonState[(ButtonFlags)i] = (packet[offset + i / 8] & (1 << (i % 8))) != 0;
        offset += ButtonBytes;
        for (int i = 1; i < (int)AxisFlags.Max; i++)
        {
            state.AxisState[(AxisFlags)i] = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(offset, 2));
            offset += 2;
        }
        Vector3 gyro = new(ReadFloat(packet, ref offset), ReadFloat(packet, ref offset), ReadFloat(packet, ref offset));
        Vector3 accel = new(ReadFloat(packet, ref offset), ReadFloat(packet, ref offset), ReadFloat(packet, ref offset));
        state.GyroState.SetGyroscope(gyro.X, gyro.Y, gyro.Z);
        state.GyroState.SetAccelerometer(accel.X, accel.Y, accel.Z);
        result = new(opCode, id, string.IsNullOrWhiteSpace(name) ? "Network Controller" : name, userIndex, sequence, state, endpoint, null);
        return true;
    }

    private static float ReadFloat(byte[] packet, ref int offset)
    {
        float value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(offset, 4)));
        offset += 4;
        return value;
    }
}
