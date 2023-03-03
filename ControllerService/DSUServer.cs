using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ControllerService.Sensors;
using Force.Crc32;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;

namespace ControllerService
{
    public enum DsState : byte
    {
        [Description("Disconnected")]
        Disconnected = 0x00,
        [Description("Reserved")]
        Reserved = 0x01,
        [Description("Connected")]
        Connected = 0x02
    };

    public enum DsConnection : byte
    {
        [Description("None")]
        None = 0x00,
        [Description("Usb")]
        Usb = 0x01,
        [Description("Bluetooth")]
        Bluetooth = 0x02
    };

    public enum DsModel : byte
    {
        [Description("None")]
        None = 0,
        [Description("DualShock 3")]
        DS3 = 1,
        [Description("DualShock 4")]
        DS4 = 2,
        [Description("Generic Gamepad")]
        Generic = 3
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
    };

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
        public const int NUMBER_SLOTS = 4;
        private Socket udpSock;
        private uint serverId;
        public bool running;
        private byte[] recvBuffer = new byte[1024];
        private SocketAsyncEventArgs[] argsList;
        private int listInd = 0;
        private ReaderWriterLockSlim poolLock = new ReaderWriterLockSlim();
        private SemaphoreSlim _pool;
        private const int ARG_BUFFER_LEN = 80;

        public DualShockPadMeta padMeta;
        private PhysicalAddress PadMacAddress;
        private int udpPacketCount = 0;

        public delegate void GetPadDetail(int padIdx, ref DualShockPadMeta meta);

        private GetPadDetail portInfoGet;

        public PrecisionTimer UpdateTimer;
        private PrecisionTimer BatteryTimer;

        private ControllerState Inputs = new();

        protected const short UPDATE_INTERVAL = 10;

        void GetPadDetailForIdx(int padIdx, ref DualShockPadMeta meta)
        {
            meta = padMeta;
        }

        public string ip;
        public int port;

        public event StartedEventHandler Started;
        public delegate void StartedEventHandler(DSUServer server);

        public event StoppedEventHandler Stopped;
        public delegate void StoppedEventHandler(DSUServer server);

        public DSUServer(string ipString, int port)
        {
            this.ip = ipString;
            this.port = port;

            if (!CommonUtils.IsTextAValidIPAddress(ip))
                this.ip = "127.0.0.1";

            PadMacAddress = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });
            portInfoGet = GetPadDetailForIdx;

            padMeta = new DualShockPadMeta()
            {
                BatteryStatus = DsBattery.Full,
                ConnectionType = DsConnection.Usb,
                IsActive = true,
                PadId = (byte)0,
                PadMacAddress = PadMacAddress,
                Model = DsModel.DS4,
                PadState = DsState.Connected
            };

            _pool = new SemaphoreSlim(ARG_BUFFER_LEN);
            argsList = new SocketAsyncEventArgs[ARG_BUFFER_LEN];
            for (int num = 0; num < ARG_BUFFER_LEN; num++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.SetBuffer(new byte[100], 0, 100);
                args.Completed += SocketEvent_Completed;
                argsList[num] = args;
            }

            BatteryTimer = new PrecisionTimer();
            BatteryTimer.SetInterval(1000);
            BatteryTimer.SetAutoResetMode(true);

            // initialize timers
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(UPDATE_INTERVAL);
            UpdateTimer.SetAutoResetMode(true);
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        private void UpdateBattery(object sender, EventArgs e)
        {
            if (!running)
                return;

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

        private void SocketEvent_Completed(object sender, SocketAsyncEventArgs e)
        {
            _pool.Release();
        }

        private void CompletedSynchronousSocketEvent()
        {
            _pool.Release();
        }

        enum MessageType
        {
            DSUC_VersionReq = 0x100000,
            DSUS_VersionRsp = 0x100000,
            DSUC_ListPorts = 0x100001,
            DSUS_PortInfo = 0x100001,
            DSUC_PadDataReq = 0x100002,
            DSUS_PadDataRsp = 0x100002,
        };

        private const ushort MaxProtocolVersion = 1001;
        class ClientRequestTimes
        {
            DateTime allPads;
            DateTime[] padIds;
            Dictionary<PhysicalAddress, DateTime> padMacs;

            public DateTime AllPadsTime { get { return allPads; } }
            public DateTime[] PadIdsTime { get { return padIds; } }
            public Dictionary<PhysicalAddress, DateTime> PadMacsTime { get { return padMacs; } }

            public ClientRequestTimes()
            {
                allPads = DateTime.MinValue;
                padIds = new DateTime[4];

                for (int i = 0; i < padIds.Length; i++)
                    padIds[i] = DateTime.MinValue;

                padMacs = new Dictionary<PhysicalAddress, DateTime>();
            }

            public void RequestPadInfo(byte regFlags, byte idToReg, PhysicalAddress macToReg)
            {
                if (regFlags == 0)
                    allPads = DateTime.UtcNow;
                else
                {
                    if ((regFlags & 0x01) != 0) //id valid
                    {
                        if (idToReg < padIds.Length)
                            padIds[idToReg] = DateTime.UtcNow;
                    }
                    if ((regFlags & 0x02) != 0) //mac valid
                    {
                        padMacs[macToReg] = DateTime.UtcNow;
                    }
                }
            }
        }

        private Dictionary<IPEndPoint, ClientRequestTimes> clients = new Dictionary<IPEndPoint, ClientRequestTimes>();

        private int BeginPacket(byte[] packetBuf, ushort reqProtocolVersion = MaxProtocolVersion)
        {
            int currIdx = 0;
            packetBuf[currIdx++] = (byte)'D';
            packetBuf[currIdx++] = (byte)'S';
            packetBuf[currIdx++] = (byte)'U';
            packetBuf[currIdx++] = (byte)'S';

            Array.Copy(BitConverter.GetBytes((ushort)reqProtocolVersion), 0, packetBuf, currIdx, 2);
            currIdx += 2;

            Array.Copy(BitConverter.GetBytes((ushort)packetBuf.Length - 16), 0, packetBuf, currIdx, 2);
            currIdx += 2;

            Array.Clear(packetBuf, currIdx, 4); //place for crc
            currIdx += 4;

            Array.Copy(BitConverter.GetBytes((uint)serverId), 0, packetBuf, currIdx, 4);
            currIdx += 4;

            return currIdx;
        }

        private void FinishPacket(byte[] packetBuf)
        {
            Array.Clear(packetBuf, 8, 4);

            uint crcCalc = Crc32Algorithm.Compute(packetBuf);
            Array.Copy(BitConverter.GetBytes((uint)crcCalc), 0, packetBuf, 8, 4);
        }

        private void SendPacket(IPEndPoint clientEP, byte[] usefulData, ushort reqProtocolVersion = MaxProtocolVersion)
        {
            byte[] packetData = new byte[usefulData.Length + 16];
            int currIdx = BeginPacket(packetData, reqProtocolVersion);
            Array.Copy(usefulData, 0, packetData, currIdx, usefulData.Length);
            FinishPacket(packetData);
            poolLock.EnterWriteLock();
            //try { udpSock.SendTo(packetData, clientEP); }
            int temp = listInd;
            listInd = ++listInd % ARG_BUFFER_LEN;
            SocketAsyncEventArgs args = argsList[temp];
            poolLock.ExitWriteLock();

            _pool.Wait();
            args.RemoteEndPoint = clientEP;
            Array.Copy(packetData, args.Buffer, packetData.Length);
            //args.SetBuffer(packetData, 0, packetData.Length);
            bool sentAsync = false;
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
                int currIdx = 0;
                if (localMsg[0] != 'D' || localMsg[1] != 'S' || localMsg[2] != 'U' || localMsg[3] != 'C')
                    return;
                else
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

                uint crcValue = BitConverter.ToUInt32(localMsg, currIdx);
                //zero out the crc32 in the packet once we got it since that's whats needed for calculation
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;

                uint crcCalc = Crc32Algorithm.Compute(localMsg);
                if (crcValue != crcCalc)
                    return;

                uint clientId = BitConverter.ToUInt32(localMsg, currIdx);
                currIdx += 4;

                uint messageType = BitConverter.ToUInt32(localMsg, currIdx);
                currIdx += 4;

                if (messageType == (uint)MessageType.DSUC_VersionReq)
                {
                    byte[] outputData = new byte[8];
                    int outIdx = 0;
                    Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_VersionRsp), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes((ushort)MaxProtocolVersion), 0, outputData, outIdx, 2);
                    outIdx += 2;
                    outputData[outIdx++] = 0;
                    outputData[outIdx++] = 0;

                    SendPacket(clientEP, outputData, 1001);
                }
                else if (messageType == (uint)MessageType.DSUC_ListPorts)
                {
                    int numPadRequests = BitConverter.ToInt32(localMsg, currIdx);
                    currIdx += 4;
                    if (numPadRequests < 0 || numPadRequests > NUMBER_SLOTS)
                        return;

                    int requestsIdx = currIdx;
                    for (int i = 0; i < numPadRequests; i++)
                    {
                        byte currRequest = localMsg[requestsIdx + i];
                        if (currRequest >= NUMBER_SLOTS)
                            return;
                    }

                    byte[] outputData = new byte[16];
                    for (byte i = 0; i < numPadRequests; i++)
                    {
                        byte currRequest = localMsg[requestsIdx + i];
                        DualShockPadMeta padData = new DualShockPadMeta();
                        portInfoGet(currRequest, ref padData);

                        int outIdx = 0;
                        Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_PortInfo), 0, outputData, outIdx, 4);
                        outIdx += 4;

                        outputData[outIdx++] = (byte)padData.PadId;
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

                        SendPacket(clientEP, outputData, 1001);
                    }
                }
                else if (messageType == (uint)MessageType.DSUC_PadDataReq)
                {
                    byte regFlags = localMsg[currIdx++];
                    byte idToReg = localMsg[currIdx++];
                    PhysicalAddress macToReg = new PhysicalAddress(new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10 });

                    lock (clients)
                    {
                        if (clients.ContainsKey(clientEP))
                            clients[clientEP].RequestPadInfo(regFlags, idToReg, macToReg);
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
                    Socket recvSock = (Socket)iar.AsyncState;

                    int msgLen = recvSock.EndReceiveFrom(iar, ref clientEP);
                    localMsg = new byte[msgLen];
                    Array.Copy(recvBuffer, localMsg, msgLen);
                }
            }
            catch
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                udpSock?.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
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
                    udpSock?.BeginReceiveFrom(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, ref newClientEP, ReceiveCallback, udpSock);
                }
            }
            catch
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                udpSock?.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

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
                IPAddress udpListenIPAddress = IPAddress.Parse(ip);
                udpSock.Bind(new IPEndPoint(udpListenIPAddress, port));
            }
            catch (SocketException)
            {
                LogManager.LogCritical("{0} couldn't listen to ip: {1} port: {2}", this.ToString(), ip, port);
                this.Stop();
                return running;
            }

            byte[] randomBuf = new byte[4];
            new Random().NextBytes(randomBuf);
            serverId = BitConverter.ToUInt32(randomBuf, 0);

            UpdateTimer.Tick += SubmitReport;
            BatteryTimer.Tick += UpdateBattery;

            running = true;

            UpdateTimer.Start();
            BatteryTimer.Start();
            StartReceive();

            LogManager.LogInformation("{0} has started. Listening to ip: {1} port: {2}", this.ToString(), ip, port);
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

            UpdateTimer.Tick -= SubmitReport;
            BatteryTimer.Tick -= UpdateBattery;

            UpdateTimer.Stop();
            BatteryTimer.Stop();

            LogManager.LogInformation("{0} has stopped", this.ToString());
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

                outputData[++outIdx] = Convert.ToByte(Inputs.ButtonState[ButtonFlags.Special]); // (hidReport.PS) ? (byte)1 : 
                outputData[++outIdx] = Convert.ToByte(Inputs.ButtonState[ButtonFlags.LeftPadClick] || Inputs.ButtonState[ButtonFlags.RightPadClick]); // (hidReport.TouchButton) ? (byte)1 : 

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
                for (int i = 0; i < 2; i++)
                {
                    var tpad = (i == 0) ? DS4Touch.LeftPadTouch : DS4Touch.RightPadTouch;

                    outputData[outIdx++] = tpad.IsActive ? (byte)1 : (byte)0;
                    outputData[outIdx++] = (byte)tpad.RawTrackingNum;
                    Array.Copy(BitConverter.GetBytes((ushort)tpad.X), 0, outputData, outIdx, 2);
                    outIdx += 2;
                    Array.Copy(BitConverter.GetBytes((ushort)tpad.Y), 0, outputData, outIdx, 2);
                    outIdx += 2;
                }

                //motion timestamp
                Array.Copy(BitConverter.GetBytes((ulong)IMU.CurrentMicroseconds), 0, outputData, outIdx, 8);

                outIdx += 8;

                //accelerometer
                if (IMU.Acceleration[XInputSensorFlags.Default] != Vector3.Zero)
                {
                    // accelXG
                    Array.Copy(BitConverter.GetBytes(IMU.Acceleration[XInputSensorFlags.Default].X), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    // accelYG
                    Array.Copy(BitConverter.GetBytes(IMU.Acceleration[XInputSensorFlags.Default].Y), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    // accelZG
                    Array.Copy(BitConverter.GetBytes(-IMU.Acceleration[XInputSensorFlags.Default].Z), 0, outputData, outIdx, 4);
                    outIdx += 4;
                }
                else
                {
                    Array.Clear(outputData, outIdx, 12);
                    outIdx += 12;
                }

                //gyroscope
                if (IMU.AngularVelocity[XInputSensorFlags.CenteredRatio] != Vector3.Zero)
                {
                    // angVelPitch
                    Array.Copy(BitConverter.GetBytes(IMU.AngularVelocity[XInputSensorFlags.CenteredRatio].X), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    // angVelYaw
                    Array.Copy(BitConverter.GetBytes(IMU.AngularVelocity[XInputSensorFlags.CenteredRatio].Y), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    // angVelRoll
                    Array.Copy(BitConverter.GetBytes(-IMU.AngularVelocity[XInputSensorFlags.CenteredRatio].Z), 0, outputData, outIdx, 4);
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

        public void SubmitReport(object sender, EventArgs e)
        {
            if (!running)
                return;

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
                        clientsList.Add(cl.Key);
                    else if ((padMeta.PadId < cl.Value.PadIdsTime.Length) &&
                             (now - cl.Value.PadIdsTime[(byte)padMeta.PadId]).TotalSeconds < TimeoutLimit)
                        clientsList.Add(cl.Key);
                    else if (cl.Value.PadMacsTime.ContainsKey(padMeta.PadMacAddress) &&
                             (now - cl.Value.PadMacsTime[padMeta.PadMacAddress]).TotalSeconds < TimeoutLimit)
                        clientsList.Add(cl.Key);
                    else //check if this client is totally dead, and remove it if so
                    {
                        bool clientOk = false;
                        for (int i = 0; i < cl.Value.PadIdsTime.Length; i++)
                        {
                            var dur = (now - cl.Value.PadIdsTime[i]).TotalSeconds;
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

                foreach (var delCl in clientsToDelete)
                {
                    clients.Remove(delCl);
                }
                clientsToDelete.Clear();
                clientsToDelete = null;
            }

            if (clientsList.Count <= 0)
                return;

            unchecked
            {
                byte[] outputData = new byte[100];
                int outIdx = BeginPacket(outputData, 1001);
                Array.Copy(BitConverter.GetBytes((uint)MessageType.DSUS_PadDataRsp), 0, outputData, outIdx, 4);
                outIdx += 4;

                outputData[outIdx++] = (byte)padMeta.PadId;
                outputData[outIdx++] = (byte)padMeta.PadState;
                outputData[outIdx++] = (byte)padMeta.Model;
                outputData[outIdx++] = (byte)padMeta.ConnectionType;
                {
                    byte[] padMac = padMeta.PadMacAddress.GetAddressBytes();
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
                else
                    FinishPacket(outputData);

                foreach (var cl in clientsList)
                {
                    //try { udpSock.SendTo(outputData, cl); }
                    int temp = 0;
                    poolLock.EnterWriteLock();
                    temp = listInd;
                    listInd = ++listInd % ARG_BUFFER_LEN;
                    SocketAsyncEventArgs args = argsList[temp];
                    poolLock.ExitWriteLock();

                    _pool.Wait();
                    args.RemoteEndPoint = cl;
                    Array.Copy(outputData, args.Buffer, outputData.Length);
                    bool sentAsync = false;
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
    }
}
