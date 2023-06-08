using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Force.Crc32;

namespace ControllerService;

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

public struct DualShockPadMeta
{
    public byte PadId;
    public DsState PadState;
    public DsConnection ConnectionType;
    public DsModel Model;
    public PhysicalAddress PadMacAddress;
    public DsBattery BatteryStatus;
    public bool IsActive;
}

public class DSUServer
{
    public delegate void GetPadDetail(int padIdx, ref DualShockPadMeta meta);

    public delegate void StartedEventHandler(DSUServer server);

    public delegate void StoppedEventHandler(DSUServer server);

    public const int NUMBER_SLOTS = 4;
    private const int ARG_BUFFER_LEN = 80;

    protected const short UPDATE_INTERVAL = 10;

    private const ushort MaxProtocolVersion = 1001;
    private readonly SemaphoreSlim _pool;
    private readonly SocketAsyncEventArgs[] argsList;

    private readonly Dictionary<IPEndPoint, ClientRequestTimes> clients = new();

    private ControllerState Inputs = new();

    public string ip;
    private int listInd;
    private readonly PhysicalAddress PadMacAddress;

    public DualShockPadMeta padMeta;
    private readonly ReaderWriterLockSlim poolLock = new();
    public int port;

    private readonly GetPadDetail portInfoGet;
    private readonly byte[] recvBuffer = new byte[1024];
    public bool running;
    private uint serverId;
    private int udpPacketCount;
    private Socket udpSock;

    public DSUServer(string ipString, int port)
    {
        ip = ipString;
        this.port = port;

        if (!CommonUtils.IsTextAValidIPAddress(ip))
            ip = "127.0.0.1";

        PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });
        portInfoGet = GetPadDetailForIdx;

        padMeta = new DualShockPadMeta
        {
            BatteryStatus = DsBattery.Full,
            ConnectionType = DsConnection.Usb,
            IsActive = true,
            PadId = 0,
            PadMacAddress = PadMacAddress,
            Model = DsModel.DS4,
            PadState = DsState.Connected
        };

        _pool = new SemaphoreSlim(ARG_BUFFER_LEN);
        argsList = new SocketAsyncEventArgs[ARG_BUFFER_LEN];
        for (var num = 0; num < ARG_BUFFER_LEN; num++)
        {
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[100], 0, 100);
            args.Completed += SocketEvent_Completed;
            argsList[num] = args;
        }
    }

    private void GetPadDetailForIdx(int padIdx, ref DualShockPadMeta meta)
    {
        meta = padMeta;
    }

    public event StartedEventHandler Started;

    public event StoppedEventHandler Stopped;

    public override string ToString()
    {
        return GetType().Name;
    }

    private void SocketEvent_Completed(object sender, SocketAsyncEventArgs e)
    {
        _pool.Release();
    }

    private void CompletedSynchronousSocketEvent()
    {
        _pool.Release();
    }

    private int BeginPacket(byte[] packetBuf, ushort reqProtocolVersion = MaxProtocolVersion)
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

    private void FinishPacket(byte[] packetBuf)
    {
        Array.Clear(packetBuf, 8, 4);

        var crcCalc = Crc32Algorithm.Compute(packetBuf);
        Array.Copy(BitConverter.GetBytes(crcCalc), 0, packetBuf, 8, 4);
    }

    private void SendPacket(IPEndPoint clientEP, byte[] usefulData, ushort reqProtocolVersion = MaxProtocolVersion)
    {
        var packetData = new byte[usefulData.Length + 16];
        var currIdx = BeginPacket(packetData, reqProtocolVersion);
        Array.Copy(usefulData, 0, packetData, currIdx, usefulData.Length);
        FinishPacket(packetData);
        poolLock.EnterWriteLock();
        //try { udpSock.SendTo(packetData, clientEP); }
        var temp = listInd;
        listInd = ++listInd % ARG_BUFFER_LEN;
        var args = argsList[temp];
        poolLock.ExitWriteLock();

        _pool.Wait();
        args.RemoteEndPoint = clientEP;
        Array.Copy(packetData, args.Buffer, packetData.Length);
        //args.SetBuffer(packetData, 0, packetData.Length);
        var sentAsync = false;
        try
        {
            sentAsync = udpSock.SendToAsync(args);
            if (!sentAsync) CompletedSynchronousSocketEvent();
        }
        catch (Exception /*e*/)
        {
        }
        finally
        {
            if (!sentAsync) CompletedSynchronousSocketEvent();
        }
    }

    private void ProcessIncoming(byte[] localMsg, IPEndPoint clientEP)
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
            {
                return;
            }

            if (packetSize < localMsg.Length)
            {
                var newMsg = new byte[packetSize];
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
                    portInfoGet(currRequest, ref padData);

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

    private void ReceiveCallback(IAsyncResult iar)
    {
        byte[] localMsg = null;
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            if (running)
            {
                //Get the received message.
                var recvSock = (Socket)iar.AsyncState;

                var msgLen = recvSock.EndReceiveFrom(iar, ref clientEP);
                localMsg = new byte[msgLen];
                Array.Copy(recvBuffer, localMsg, msgLen);
            }
        }
        catch
        {
            var IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpSock?.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
        }

        //Start another receive as soon as we copied the data
        StartReceive();

        //Process the data if its valid
        if (localMsg is not null)
            ProcessIncoming(localMsg, (IPEndPoint)clientEP);
    }

    private void StartReceive()
    {
        try
        {
            if (running)
            {
                //Start listening for a new message.
                EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
                udpSock?.BeginReceiveFrom(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, ref newClientEP,
                    ReceiveCallback, udpSock);
            }
        }
        catch
        {
            var IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            var SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpSock?.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);

            StartReceive();
        }
    }

    public bool Start()
    {
        if (running)
            Stop();

        udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            var udpListenIPAddress = IPAddress.Parse(ip);
            udpSock.Bind(new IPEndPoint(udpListenIPAddress, port));
        }
        catch (SocketException)
        {
            LogManager.LogCritical("{0} couldn't listen to ip: {1} port: {2}", ToString(), ip, port);
            Stop();
            return running;
        }

        var randomBuf = new byte[4];
        new Random().NextBytes(randomBuf);
        serverId = BitConverter.ToUInt32(randomBuf, 0);

        TimerManager.Tick += Tick;

        running = true;

        StartReceive();

        LogManager.LogInformation("{0} has started. Listening to ip: {1} port: {2}", ToString(), ip, port);
        Started?.Invoke(this);

        return running;
    }

    public void Stop()
    {
        if (udpSock is not null)
        {
            udpSock.Close();
            udpSock = null;
        }

        running = false;

        TimerManager.Tick -= Tick;

        LogManager.LogInformation("{0} has stopped", ToString());
        Stopped?.Invoke(this);
    }

    public void UpdateInputs(ControllerState inputs)
    {
        Inputs = inputs;
    }

    private bool ReportToBuffer(byte[] outputData, ref int outIdx)
    {
        unchecked
        {
            outputData[outIdx] = 0;

            if (Inputs.ButtonState[ButtonFlags.DPadLeft]) outputData[outIdx] |= 0x80;
            if (Inputs.ButtonState[ButtonFlags.DPadDown]) outputData[outIdx] |= 0x40;
            if (Inputs.ButtonState[ButtonFlags.DPadRight]) outputData[outIdx] |= 0x20;
            if (Inputs.ButtonState[ButtonFlags.DPadUp]) outputData[outIdx] |= 0x10;

            if (Inputs.ButtonState[ButtonFlags.Start]) outputData[outIdx] |= 0x08;
            if (Inputs.ButtonState[ButtonFlags.RightThumb]) outputData[outIdx] |= 0x04;
            if (Inputs.ButtonState[ButtonFlags.LeftThumb]) outputData[outIdx] |= 0x02;
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
                Convert.ToByte(Inputs.ButtonState[ButtonFlags.Special]); // (hidReport.PS) ? (byte)1 : 
            outputData[++outIdx] = Convert.ToByte(Inputs.ButtonState[ButtonFlags.LeftPadClick] ||
                                                  Inputs.ButtonState[
                                                      ButtonFlags
                                                          .RightPadClick]); // (hidReport.TouchButton) ? (byte)1 : 

            //Left stick
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftThumbX]);
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.LeftThumbY]);
            outputData[outIdx] = (byte)(byte.MaxValue - outputData[outIdx]); //invert Y by convention

            //Right stick
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightThumbX]);
            outputData[++outIdx] = InputUtils.NormalizeXboxInput(Inputs.AxisState[AxisFlags.RightThumbY]);
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

            outputData[++outIdx] = (byte)Inputs.AxisState[AxisFlags.L2];
            outputData[++outIdx] = (byte)Inputs.AxisState[AxisFlags.R2];

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

            //accelerometer
            if (IMU.Acceleration.TryGetValue(XInputSensorFlags.Default, out var AccelerationVector) &&
                AccelerationVector != Vector3.Zero)
            {
                // accelXG
                Array.Copy(BitConverter.GetBytes(AccelerationVector.X), 0, outputData, outIdx, 4);
                outIdx += 4;
                // accelYG
                Array.Copy(BitConverter.GetBytes(AccelerationVector.Y), 0, outputData, outIdx, 4);
                outIdx += 4;
                // accelZG
                Array.Copy(BitConverter.GetBytes(-AccelerationVector.Z), 0, outputData, outIdx, 4);
                outIdx += 4;
            }
            else
            {
                Array.Clear(outputData, outIdx, 12);
                outIdx += 12;
            }

            //gyroscope
            if (IMU.AngularVelocity.TryGetValue(XInputSensorFlags.CenteredRatio, out var AngularVector) &&
                AngularVector != Vector3.Zero)
            {
                // angVelPitch
                Array.Copy(BitConverter.GetBytes(AngularVector.X), 0, outputData, outIdx, 4);
                outIdx += 4;
                // angVelYaw
                Array.Copy(BitConverter.GetBytes(AngularVector.Y), 0, outputData, outIdx, 4);
                outIdx += 4;
                // angVelRoll
                Array.Copy(BitConverter.GetBytes(-AngularVector.Z), 0, outputData, outIdx, 4);
                outIdx += 4;
            }
            else
            {
                Array.Clear(outputData, outIdx, 12);
                outIdx += 12;
            }
        }

        return true;
    }

    public void Tick(long ticks)
    {
        if (!running)
            return;

        // only update every one second
        if (ticks % 1000 == 0)
        {
            var ChargeStatus = SystemInformation.PowerStatus.BatteryChargeStatus;

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

        var clientsList = new List<IPEndPoint>();
        var now = DateTime.UtcNow;
        lock (clients)
        {
            var clientsToDelete = new List<IPEndPoint>();

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

            if (!ReportToBuffer(outputData, ref outIdx))
                return;
            FinishPacket(outputData);

            foreach (var cl in clientsList)
            {
                //try { udpSock.SendTo(outputData, cl); }
                var temp = 0;
                poolLock.EnterWriteLock();
                temp = listInd;
                listInd = ++listInd % ARG_BUFFER_LEN;
                var args = argsList[temp];
                poolLock.ExitWriteLock();

                _pool.Wait();
                args.RemoteEndPoint = cl;
                Array.Copy(outputData, args.Buffer, outputData.Length);
                var sentAsync = false;
                try
                {
                    sentAsync = udpSock.SendToAsync(args);
                }
                catch (SocketException)
                {
                }
                finally
                {
                    if (!sentAsync) CompletedSynchronousSocketEvent();
                }
            }
        }

        clientsList.Clear();
    }

    private enum MessageType
    {
        DSUC_VersionReq = 0x100000,
        DSUS_VersionRsp = 0x100000,
        DSUC_ListPorts = 0x100001,
        DSUS_PortInfo = 0x100001,
        DSUC_PadDataReq = 0x100002,
        DSUS_PadDataRsp = 0x100002
    }

    private class ClientRequestTimes
    {
        public ClientRequestTimes()
        {
            AllPadsTime = DateTime.MinValue;
            PadIdsTime = new DateTime[4];

            for (var i = 0; i < PadIdsTime.Length; i++)
                PadIdsTime[i] = DateTime.MinValue;

            PadMacsTime = new Dictionary<PhysicalAddress, DateTime>();
        }

        public DateTime AllPadsTime { get; private set; }

        public DateTime[] PadIdsTime { get; }

        public Dictionary<PhysicalAddress, DateTime> PadMacsTime { get; }

        public void RequestPadInfo(byte regFlags, byte idToReg, PhysicalAddress macToReg)
        {
            if (regFlags == 0)
            {
                AllPadsTime = DateTime.UtcNow;
            }
            else
            {
                if ((regFlags & 0x01) != 0) //id valid
                    if (idToReg < PadIdsTime.Length)
                        PadIdsTime[idToReg] = DateTime.UtcNow;
                if ((regFlags & 0x02) != 0) //mac valid
                    PadMacsTime[macToReg] = DateTime.UtcNow;
            }
        }
    }
}