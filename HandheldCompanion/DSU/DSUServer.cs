using Force.Crc32;
using HandheldCompanion.Controllers;
using HandheldCompanion.DSU;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace HandheldCompanion;

public enum DsState : byte
{
    [Description("Disconnected")] Disconnected = 0x00,
    [Description("Reserved")] Reserved = 0x01,
    [Description("Connected")] Connected = 0x02
}

public enum DsConnection : byte
{
    [Description("None")] None = 0x00,
    [Description("Usb")] Usb = 0x01,
    [Description("Bluetooth")] Bluetooth = 0x02
}

public enum DsModel : byte
{
    [Description("None")] None = 0,
    [Description("DualShock 3")] DS3 = 1,
    [Description("DualShock 4")] DS4 = 2,
    [Description("Generic Gamepad")] Generic = 3
}

public enum DsBattery : byte
{
    None = 0x00,
    Dying = 0x01,
    Low = 0x02,
    Medium = 0x03,
    High = 0x04,
    Full = 0x05,
    Charging = 0xEE,
    Charged = 0xEF
}

public static class DSUServer
{
    private const byte NUMBER_SLOTS = 4;
    private const int ARG_BUFFER_LEN = 80;
    private const ushort MaxProtocolVersion = 1001;

    private static SemaphoreSlim _pool;
    private static byte[][] dataBuffers;

    private static readonly Dictionary<IPEndPoint, ClientRequestTimes> clients = [];
    private static readonly DualShockPadMeta[] padMetas = new DualShockPadMeta[NUMBER_SLOTS];
    private static readonly ReaderWriterLockSlim poolLock = new();
    private static readonly byte[] recvBuffer = new byte[1024];

    private const int serverPort = 26760;
    private static uint serverId;
    private static int udpPacketCount;
    private static Socket udpSock;

    public static bool IsInitialized;

    #region events
    public delegate void StartedEventHandler();
    public static event StartedEventHandler Started;

    public delegate void StoppedEventHandler();
    public static event StoppedEventHandler Stopped;
    #endregion

    static DSUServer()
    {
        _pool = new SemaphoreSlim(ARG_BUFFER_LEN);
        dataBuffers = new byte[ARG_BUFFER_LEN][];
        for (int num = 0; num < ARG_BUFFER_LEN; num++)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += SocketEvent_AsyncCompleted;
            dataBuffers[num] = new byte[100];
        }

        for (byte padIdx = 0; padIdx < NUMBER_SLOTS; padIdx++)
        {
            byte address = (byte)(0x10 + padIdx);

            padMetas[padIdx] = new DualShockPadMeta()
            {
                BatteryStatus = DsBattery.Full,
                ConnectionType = DsConnection.Usb,
                IsActive = true,
                PadId = padIdx,
                PadMacAddress = new PhysicalAddress([address, address, address, address, address, address]),
                Model = DsModel.DS4,
                PadState = DsState.Connected
            };
        }
    }

    private static void GetPadDetailForIdx(int padIdx, ref DualShockPadMeta meta)
    {
        meta = padMetas[padIdx];
    }

    private static void SocketEvent_AsyncCompleted(object sender, SocketAsyncEventArgs e)
    {
        _pool.Release();
        e.Dispose();
    }

    private static void CompletedSynchronousSocketEvent(SocketAsyncEventArgs args)
    {
        _pool.Release();
        args.Dispose();
    }

    private static int BeginPacket(byte[] packetBuf, ushort reqProtocolVersion = MaxProtocolVersion)
    {
        var currIdx = 0;
        packetBuf[currIdx++] = (byte)'D';
        packetBuf[currIdx++] = (byte)'S';
        packetBuf[currIdx++] = (byte)'U';
        packetBuf[currIdx++] = (byte)'S';

        Array.Copy(BitConverter.GetBytes(reqProtocolVersion), 0, packetBuf, currIdx, 2);
        currIdx += 2;

        Array.Copy(BitConverter.GetBytes((ushort)packetBuf.Length - 16), 0, packetBuf, currIdx, 2);
        currIdx += 2;

        Array.Clear(packetBuf, currIdx, 4); //place for crc
        currIdx += 4;

        Array.Copy(BitConverter.GetBytes(serverId), 0, packetBuf, currIdx, 4);
        currIdx += 4;

        return currIdx;
    }

    private static void FinishPacket(byte[] packetBuf)
    {
        Array.Clear(packetBuf, 8, 4);

        var crcCalc = Crc32Algorithm.Compute(packetBuf);
        Array.Copy(BitConverter.GetBytes(crcCalc), 0, packetBuf, 8, 4);
    }

    private static int listInd;
    private static void SendPacket(IPEndPoint clientEP, byte[] usefulData, ushort reqProtocolVersion = MaxProtocolVersion)
    {
        byte[] packetData = new byte[usefulData.Length + 16];
        int currIdx = BeginPacket(packetData, reqProtocolVersion);
        Array.Copy(usefulData, 0, packetData, currIdx, usefulData.Length);
        FinishPacket(packetData);

        //try { udpSock.SendTo(packetData, clientEP); }
        int temp = 0;
        poolLock.EnterWriteLock();
        temp = listInd;
        listInd = ++listInd % ARG_BUFFER_LEN;
        SocketAsyncEventArgs args = new SocketAsyncEventArgs()
        {
            RemoteEndPoint = clientEP,
        };
        args.SetBuffer(dataBuffers[temp], 0, 100);
        args.Completed += SocketEvent_AsyncCompleted;
        poolLock.ExitWriteLock();

        _pool.Wait();
        Array.Copy(packetData, args.Buffer, packetData.Length);
        //args.SetBuffer(packetData, 0, packetData.Length);
        bool sentAsync = false;
        try
        {
            sentAsync = udpSock.SendToAsync(args);
            //if (!sentAsync) CompletedSynchronousSocketEvent();
        }
        catch (Exception /*e*/) { }
        finally
        {
            if (!sentAsync) CompletedSynchronousSocketEvent(args);
        }
    }

    private static void ProcessIncoming(byte[] localMsg, IPEndPoint clientEP)
    {
        try
        {
            var currIdx = 0;
            if (localMsg[0] != 'D' || localMsg[1] != 'S' || localMsg[2] != 'U' || localMsg[3] != 'C')
                return;
            currIdx += 4;

            uint protocolVer = BitConverter.ToUInt16(localMsg, currIdx);
            currIdx += 2;

            if (protocolVer > MaxProtocolVersion)
                return;

            uint packetSize = BitConverter.ToUInt16(localMsg, currIdx);
            currIdx += 2;

            if (packetSize < 0)
                return;

            packetSize += 16; //size of header
            if (packetSize > localMsg.Length)
                return;
            else if (packetSize < localMsg.Length)
            {
                byte[] newMsg = new byte[packetSize];
                Array.Copy(localMsg, newMsg, packetSize);
                localMsg = newMsg;
            }

            var crcValue = BitConverter.ToUInt32(localMsg, currIdx);
            //zero out the crc32 in the packet once we got it since that's whats needed for calculation
            localMsg[currIdx++] = 0;
            localMsg[currIdx++] = 0;
            localMsg[currIdx++] = 0;
            localMsg[currIdx++] = 0;

            var crcCalc = Crc32Algorithm.Compute(localMsg);
            if (crcValue != crcCalc)
                return;

            var clientId = BitConverter.ToUInt32(localMsg, currIdx);
            currIdx += 4;

            var messageType = BitConverter.ToUInt32(localMsg, currIdx);
            currIdx += 4;

            if (messageType == (uint)MessageType.DSUC_VersionReq)
            {
                var outputData = new byte[8];
                var outIdx = 0;
                Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_VersionRsp), 0, outputData, outIdx, 4);
                outIdx += 4;
                Array.Copy(BitConverter.GetBytes(MaxProtocolVersion), 0, outputData, outIdx, 2);
                outIdx += 2;
                outputData[outIdx++] = 0;
                outputData[outIdx++] = 0;

                SendPacket(clientEP, outputData);
            }
            else if (messageType == (uint)MessageType.DSUC_ListPorts)
            {
                var numPadRequests = BitConverter.ToInt32(localMsg, currIdx);
                currIdx += 4;
                if (numPadRequests < 0 || numPadRequests > NUMBER_SLOTS)
                    return;

                var requestsIdx = currIdx;
                for (var i = 0; i < numPadRequests; i++)
                {
                    var currRequest = localMsg[requestsIdx + i];
                    if (currRequest >= NUMBER_SLOTS)
                        return;
                }

                var outputData = new byte[16];
                for (byte i = 0; i < numPadRequests; i++)
                {
                    var currRequest = localMsg[requestsIdx + i];
                    var padData = new DualShockPadMeta();
                    GetPadDetailForIdx(currRequest, ref padData);

                    var outIdx = 0;
                    Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_PortInfo), 0, outputData, outIdx, 4);
                    outIdx += 4;

                    outputData[outIdx++] = padData.PadId;
                    outputData[outIdx++] = (byte)padData.PadState;
                    outputData[outIdx++] = (byte)padData.Model;
                    outputData[outIdx++] = (byte)padData.ConnectionType;

                    byte[] addressBytes = null;
                    if (padData.PadMacAddress is not null)
                        addressBytes = padData.PadMacAddress.GetAddressBytes();

                    if (addressBytes is not null && addressBytes.Length == 6)
                    {
                        outputData[outIdx++] = addressBytes[0];
                        outputData[outIdx++] = addressBytes[1];
                        outputData[outIdx++] = addressBytes[2];
                        outputData[outIdx++] = addressBytes[3];
                        outputData[outIdx++] = addressBytes[4];
                        outputData[outIdx++] = addressBytes[5];
                    }
                    else
                    {
                        outputData[outIdx++] = 0;
                        outputData[outIdx++] = 0;
                        outputData[outIdx++] = 0;
                        outputData[outIdx++] = 0;
                        outputData[outIdx++] = 0;
                        outputData[outIdx++] = 0;
                    }

                    outputData[outIdx++] = (byte)padData.BatteryStatus;
                    outputData[outIdx++] = 0;

                    SendPacket(clientEP, outputData);
                }
            }
            else if (messageType == (uint)MessageType.DSUC_PadDataReq)
            {
                var regFlags = localMsg[currIdx++];
                var idToReg = localMsg[currIdx++];
                var macToReg = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });

                lock (clients)
                {
                    if (clients.TryGetValue(clientEP, out var client))
                    {
                        client.RequestPadInfo(regFlags, idToReg, macToReg);
                    }
                    else
                    {
                        var clientTimes = new ClientRequestTimes();
                        clientTimes.RequestPadInfo(regFlags, idToReg, macToReg);
                        clients[clientEP] = clientTimes;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static void ReceiveCallback(IAsyncResult iar)
    {
        byte[] localMsg = null;
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            //Get the received message.
            Socket recvSock = (Socket)iar.AsyncState;
            int msgLen = recvSock.EndReceiveFrom(iar, ref clientEP);

            localMsg = new byte[msgLen];
            Array.Copy(recvBuffer, localMsg, msgLen);
        }
        catch (SocketException)
        {
            if (IsInitialized)
            {
                ResetUDPConn();
            }
        }
        catch (Exception /*e*/) { }

        //Start another receive as soon as we copied the data
        StartReceive();

        //Process the data if its valid
        if (localMsg != null)
            ProcessIncoming(localMsg, (IPEndPoint)clientEP);
    }

    private static void StartReceive()
    {
        try
        {
            if (IsInitialized)
            {
                //Start listening for a new message.
                EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
                udpSock.BeginReceiveFrom(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, ref newClientEP, ReceiveCallback, udpSock);
            }
        }
        catch (SocketException /*ex*/)
        {
            if (IsInitialized)
            {
                ResetUDPConn();
                StartReceive();
            }
        }
        catch (Exception /*ex*/) { }
    }

    /// <summary>
    /// Used to send CONNRESET ioControlCode to Socket used for UDP Server.
    /// Frees Socket from potentially firing SocketException instances after a client
    /// connection is terminated. Avoids memory leak
    /// </summary>
    private static void ResetUDPConn()
    {
        if (udpSock is null)
            return;

        uint IOC_IN = 0x80000000;
        uint IOC_VENDOR = 0x18000000;
        uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

        try
        {
            udpSock.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
        }
        catch (ObjectDisposedException) { }
    }

    public static bool Start()
    {
        try
        {
            udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSock.Bind(new IPEndPoint(IPAddress.Any, serverPort));
        }
        catch (SocketException)
        {
            LogManager.LogCritical("DSUServer couldn't listen to port: {0}", serverPort);
            Stop();
            return IsInitialized;
        }
        catch (Exception /*ex*/) { }

        var randomBuf = new byte[4];
        new Random().NextBytes(randomBuf);
        serverId = BitConverter.ToUInt32(randomBuf, 0);

        TimerManager.Tick += Tick;

        IsInitialized = true;

        StartReceive();

        LogManager.LogInformation("DSUServer has started. Listening to port: {0}", serverPort);
        Started?.Invoke();

        return IsInitialized;
    }

    public static void Stop()
    {
        if (udpSock is not null)
        {
            if (udpSock.Connected)
                udpSock.Disconnect(true);

            udpSock.Close();
            udpSock.Dispose();
            udpSock = null;
        }

        IsInitialized = false;

        TimerManager.Tick -= Tick;

        LogManager.LogInformation("DSUServer has stopped");
        Stopped?.Invoke();
    }

    private static ControllerState Inputs = new();
    private static Dictionary<byte, GamepadMotion> GamepadMotions = new();

    public static void UpdateInputs(ControllerState inputs, Dictionary<byte, GamepadMotion> gamepadMotions)
    {
        Inputs = inputs;
        GamepadMotions = gamepadMotions;
    }

    private static bool ReportToBuffer(byte[] outputData, ref int outIdx, byte padIdx)
    {
        unchecked
        {
            outputData[outIdx] = 0;

            if (Inputs.ButtonState[ButtonFlags.DPadLeft]) outputData[outIdx] |= 0x80;
            if (Inputs.ButtonState[ButtonFlags.DPadDown]) outputData[outIdx] |= 0x40;
            if (Inputs.ButtonState[ButtonFlags.DPadRight]) outputData[outIdx] |= 0x20;
            if (Inputs.ButtonState[ButtonFlags.DPadUp]) outputData[outIdx] |= 0x10;

            if (Inputs.ButtonState[ButtonFlags.Start]) outputData[outIdx] |= 0x08;
            if (Inputs.ButtonState[ButtonFlags.RightStickClick]) outputData[outIdx] |= 0x04;
            if (Inputs.ButtonState[ButtonFlags.LeftStickClick]) outputData[outIdx] |= 0x02;
            if (Inputs.ButtonState[ButtonFlags.Back]) outputData[outIdx] |= 0x01;

            outputData[++outIdx] = 0;

            if (Inputs.ButtonState[ButtonFlags.B1]) outputData[outIdx] |= 0x40;
            if (Inputs.ButtonState[ButtonFlags.B2]) outputData[outIdx] |= 0x20;
            if (Inputs.ButtonState[ButtonFlags.B3]) outputData[outIdx] |= 0x80;
            if (Inputs.ButtonState[ButtonFlags.B4]) outputData[outIdx] |= 0x10;

            if (Inputs.ButtonState[ButtonFlags.R1]) outputData[outIdx] |= 0x08;
            if (Inputs.ButtonState[ButtonFlags.L1]) outputData[outIdx] |= 0x04;
            if (Inputs.AxisState[AxisFlags.R2] == byte.MaxValue) outputData[outIdx] |= 0x02;
            if (Inputs.AxisState[AxisFlags.L2] == byte.MaxValue) outputData[outIdx] |= 0x01;

            outputData[++outIdx] =
                Convert.ToByte(Inputs.ButtonState[ButtonFlags.Special]); // (hidReport.PS) ? (byte)1 : (byte)0
            outputData[++outIdx] = Convert.ToByte(Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick]); // (hidReport.TouchButton) ? (byte)1 : (byte)0

            //Left stick
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftStickX]);
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftStickY]);
            outputData[outIdx] = (byte)(byte.MaxValue - outputData[outIdx]); //invert Y by convention

            //Right stick
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightStickX]);
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightStickY]);
            outputData[outIdx] = (byte)(byte.MaxValue - outputData[outIdx]); //invert Y by convention

            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.DPadLeft] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.DPadDown] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.DPadRight] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.DPadUp] ? (byte)0xFF : (byte)0x00;

            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.B1] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.B2] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.B3] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.B4] ? (byte)0xFF : (byte)0x00;

            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.R1] ? (byte)0xFF : (byte)0x00;
            outputData[++outIdx] = Inputs.ButtonState[ButtonFlags.L1] ? (byte)0xFF : (byte)0x00;

            outputData[++outIdx] = (byte)Inputs.AxisState[AxisFlags.R2];
            outputData[++outIdx] = (byte)Inputs.AxisState[AxisFlags.L2];

            outIdx++;

            //DS4 only: touchpad points
            for (var i = 0; i < 2; i++)
            {
                var tpad = i == 0 ? DS4Touch.LeftPadTouch : DS4Touch.RightPadTouch;

                outputData[outIdx++] = tpad.IsActive ? (byte)1 : (byte)0;
                outputData[outIdx++] = (byte)tpad.RawTrackingNum;
                Array.Copy(BitConverter.GetBytes((ushort)tpad.X), 0, outputData, outIdx, 2);
                outIdx += 2;
                Array.Copy(BitConverter.GetBytes((ushort)tpad.Y), 0, outputData, outIdx, 2);
                outIdx += 2;
            }

            //motion timestamp
            Array.Copy(BitConverter.GetBytes((ulong)TimerManager.GetElapsedSeconds()), 0, outputData, outIdx, 8);
            outIdx += 8;

            float gyroX = 0.0f, gyroY = 0.0f, gyroZ = 0.0f;
            float accelX = 0.0f, accelY = 0.0f, accelZ = 0.0f;
            switch (padIdx)
            {
                default:
                    gyroX = Inputs.GyroState.Gyroscope[GyroState.SensorState.DSU].X;
                    gyroY = Inputs.GyroState.Gyroscope[GyroState.SensorState.DSU].Y;
                    gyroZ = Inputs.GyroState.Gyroscope[GyroState.SensorState.DSU].Z;

                    accelX = Inputs.GyroState.Accelerometer[GyroState.SensorState.DSU].X;
                    accelY = Inputs.GyroState.Accelerometer[GyroState.SensorState.DSU].Y;
                    accelZ = Inputs.GyroState.Accelerometer[GyroState.SensorState.DSU].Z;
                    break;

                case 1:
                case 2:
                    byte gamepadMotionIdx = (byte)(padIdx - 1);
                    if (GamepadMotions.TryGetValue(gamepadMotionIdx, out GamepadMotion gamepadMotion))
                    {
                        gamepadMotion.GetCalibratedGyro(out gyroX, out gyroY, out gyroZ);
                        gamepadMotion.GetGravity(out accelX, out accelY, out accelZ);
                    }
                    break;

                case 3:
                    // do nothing
                    break;
            }

            // Accelerometer
            Array.Copy(BitConverter.GetBytes(accelX), 0, outputData, outIdx, 4);
            outIdx += 4;
            Array.Copy(BitConverter.GetBytes(accelY), 0, outputData, outIdx, 4);
            outIdx += 4;
            Array.Copy(BitConverter.GetBytes(accelZ), 0, outputData, outIdx, 4);
            outIdx += 4;

            // Gyroscope
            Array.Copy(BitConverter.GetBytes(gyroX), 0, outputData, outIdx, 4);
            outIdx += 4;
            Array.Copy(BitConverter.GetBytes(-gyroY), 0, outputData, outIdx, 4);
            outIdx += 4;
            Array.Copy(BitConverter.GetBytes(-gyroZ), 0, outputData, outIdx, 4);
            outIdx += 4;
        }

        return true;
    }

    public static void Tick(long ticks, float delta)
    {
        if (!IsInitialized)
            return;

        // only update every one second
        for (byte padIdx = 0; padIdx < NUMBER_SLOTS; padIdx++)
        {
            DualShockPadMeta padMeta = padMetas[padIdx];

            if (ticks % 1000 == 0)
            {
                BatteryChargeStatus ChargeStatus = SystemInformation.PowerStatus.BatteryChargeStatus;

                if (ChargeStatus.HasFlag(BatteryChargeStatus.Charging))
                    padMeta.BatteryStatus = DsBattery.Charging;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery))
                    padMeta.BatteryStatus = DsBattery.None;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.High))
                    padMeta.BatteryStatus = DsBattery.High;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.Low))
                    padMeta.BatteryStatus = DsBattery.Low;
                else if (ChargeStatus.HasFlag(BatteryChargeStatus.Critical))
                    padMeta.BatteryStatus = DsBattery.Dying;
                else
                    padMeta.BatteryStatus = DsBattery.Medium;
            }

            // update status
            padMeta.IsActive = true; // fixme ?

            List<IPEndPoint>? clientsList = new List<IPEndPoint>();
            DateTime now = DateTime.UtcNow;
            lock (clients)
            {
                List<IPEndPoint>? clientsToDelete = new List<IPEndPoint>();

                foreach (var cl in clients)
                {
                    const double TimeoutLimit = 5;

                    if ((now - cl.Value.AllPadsTime).TotalSeconds < TimeoutLimit)
                    {
                        clientsList.Add(cl.Key);
                    }
                    else if (padMeta.PadId < cl.Value.PadIdsTime.Length &&
                             (now - cl.Value.PadIdsTime[padMeta.PadId]).TotalSeconds < TimeoutLimit)
                    {
                        clientsList.Add(cl.Key);
                    }
                    else if (cl.Value.PadMacsTime.TryGetValue(padMeta.PadMacAddress, out var padTime) &&
                             (now - padTime).TotalSeconds < TimeoutLimit)
                    {
                        clientsList.Add(cl.Key);
                    }
                    else //check if this client is totally dead, and remove it if so
                    {
                        var clientOk = false;
                        foreach (var t in cl.Value.PadIdsTime)
                        {
                            var dur = (now - t).TotalSeconds;
                            if (dur < TimeoutLimit)
                            {
                                clientOk = true;
                                break;
                            }
                        }

                        if (!clientOk)
                        {
                            foreach (var dict in cl.Value.PadMacsTime)
                            {
                                var dur = (now - dict.Value).TotalSeconds;
                                if (dur < TimeoutLimit)
                                {
                                    clientOk = true;
                                    break;
                                }
                            }

                            if (!clientOk)
                                clientsToDelete.Add(cl.Key);
                        }
                    }
                }

                foreach (var delCl in clientsToDelete) clients.Remove(delCl);
                clientsToDelete.Clear();
                clientsToDelete = null;
            }

            if (clientsList.Count <= 0)
                return;

            unchecked
            {
                var outputData = new byte[100];
                var outIdx = BeginPacket(outputData);
                Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_PadDataRsp), 0, outputData, outIdx, 4);
                outIdx += 4;

                outputData[outIdx++] = padMeta.PadId;
                outputData[outIdx++] = (byte)padMeta.PadState;
                outputData[outIdx++] = (byte)padMeta.Model;
                outputData[outIdx++] = (byte)padMeta.ConnectionType;
                {
                    var padMac = padMeta.PadMacAddress.GetAddressBytes();
                    outputData[outIdx++] = padMac[0];
                    outputData[outIdx++] = padMac[1];
                    outputData[outIdx++] = padMac[2];
                    outputData[outIdx++] = padMac[3];
                    outputData[outIdx++] = padMac[4];
                    outputData[outIdx++] = padMac[5];
                }
                outputData[outIdx++] = (byte)padMeta.BatteryStatus;
                outputData[outIdx++] = padMeta.IsActive ? (byte)1 : (byte)0;

                Array.Copy(BitConverter.GetBytes((uint)udpPacketCount++), 0, outputData, outIdx, 4);
                outIdx += 4;

                if (!ReportToBuffer(outputData, ref outIdx, padIdx))
                    return;
                FinishPacket(outputData);

                foreach (var cl in clientsList)
                {
                    int temp = 0;
                    poolLock.EnterWriteLock();
                    temp = listInd;
                    listInd = ++listInd % ARG_BUFFER_LEN;
                    SocketAsyncEventArgs args = new SocketAsyncEventArgs()
                    {
                        RemoteEndPoint = cl,
                    };
                    args.SetBuffer(dataBuffers[temp], 0, 100);
                    args.Completed += SocketEvent_AsyncCompleted;
                    poolLock.ExitWriteLock();

                    _pool.Wait();
                    Array.Copy(outputData, args.Buffer, outputData.Length);
                    bool sentAsync = false;
                    try
                    {
                        bool sendAsync = udpSock.SendToAsync(args);
                    }
                    catch (SocketException /*ex*/) { }
                    catch (Exception /*ex*/) { }
                    finally
                    {
                        if (!sentAsync) CompletedSynchronousSocketEvent(args);
                    }
                }
            }

            clientsList.Clear();
            clientsList = null;
        }
    }
}