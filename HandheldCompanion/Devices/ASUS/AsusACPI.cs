using HandheldCompanion.Shared;
using System;
using System.Runtime.InteropServices;

public enum AsusFan
{
    CPU = 0,
    GPU = 1,
    Mid = 2,
    XGM = 3
}

public enum AsusMode
{
    Performance = 0,
    Turbo = 1,
    Silent = 2,
    Manual = 4,
}

public enum AsusGPU
{
    Eco = 0,
    Standard = 1,
    Ultimate = 2
}

namespace HandheldCompanion.Devices.ASUS
{
    public static class AsusACPI
    {
        const string FILE_NAME = @"\\.\\ATKACPI";
        const uint CONTROL_CODE = 0x0022240C;

        const uint DSTS = 0x53545344;
        const uint DEVS = 0x53564544;
        const uint INIT = 0x54494E49;
        const uint WDOG = 0x474F4457;

        public const uint GPUEco = 0x00090020;
        public const uint GPUXGConnected = 0x00090018;
        public const uint GPUXG = 0x00090019;

        public const uint DevsCPUFan = 0x00110022;
        public const uint DevsGPUFan = 0x00110023;

        public const uint DevsCPUFanCurve = 0x00110024;
        public const uint DevsGPUFanCurve = 0x00110025;
        public const uint DevsMidFanCurve = 0x00110032;

        public const uint BatteryLimit = 0x00120057;

        public const uint CPU_Fan = 0x00110013;
        public const uint GPU_Fan = 0x00110014;
        public const uint Mid_Fan = 0x00110031;

        /// <summary>
        /// SPL (sustained limit) / PL1
        /// </summary>
        public const int PPT_APUA3 = 0x001200A3;

        /// <summary>
        /// sPPT (short boost limit) / PL2
        /// </summary>
        public const int PPT_APUA0 = 0x001200A0;

        /// <summary>
        /// fPPT (fast boost limit)
        /// </summary>
        public const int PPT_APUC1 = 0x001200C1;

        public const uint PerformanceMode = 0x00120075; // Performance modes

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            byte[] lpInBuffer,
            uint nInBufferSize,
            byte[] lpOutBuffer,
            uint nOutBufferSize,
            ref uint lpBytesReturned,
            IntPtr lpOverlapped
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_SHARE_READ = 1;
        private const uint FILE_SHARE_WRITE = 2;

        private static IntPtr handle;

        // Event handling attempt

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

        static AsusACPI()
        {
            handle = CreateFile(
                FILE_NAME,
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero
            );

            if (!IsOpen)
            {
                LogManager.LogError("Can't connect to Asus ACPI");
                return;
            }
        }

        public static bool IsOpen => handle != new IntPtr(-1);

        private static void Control(uint dwIoControlCode, byte[] lpInBuffer, byte[] lpOutBuffer)
        {
            uint lpBytesReturned = 0;
            DeviceIoControl(
                handle,
                dwIoControlCode,
                lpInBuffer,
                (uint)lpInBuffer.Length,
                lpOutBuffer,
                (uint)lpOutBuffer.Length,
                ref lpBytesReturned,
                IntPtr.Zero
            );
        }

        public static void Close()
        {
            if (IsOpen)
                CloseHandle(handle);
        }

        public static byte[] CallMethod(uint MethodID, byte[] args)
        {
            byte[] acpiBuf = new byte[8 + args.Length];
            byte[] outBuffer = new byte[16];

            BitConverter.GetBytes(MethodID).CopyTo(acpiBuf, 0);
            BitConverter.GetBytes((uint)args.Length).CopyTo(acpiBuf, 4);
            Array.Copy(args, 0, acpiBuf, 8, args.Length);

            Control(CONTROL_CODE, acpiBuf, outBuffer);

            return outBuffer;
        }

        public static byte[] DeviceInit()
        {
            byte[] args = new byte[8];
            return CallMethod(INIT, args);
        }

        public static byte[] DeviceWatchDog()
        {
            byte[] args = new byte[8];
            return CallMethod(WDOG, args);
        }

        public static int DeviceSet(uint DeviceID, int Status)
        {
            byte[] args = new byte[8];
            BitConverter.GetBytes(DeviceID).CopyTo(args, 0);
            BitConverter.GetBytes((uint)Status).CopyTo(args, 4);

            byte[] status = CallMethod(DEVS, args);
            int result = BitConverter.ToInt32(status, 0);

            return result;
        }

        public static int DeviceSet(uint DeviceID, byte[] Params, string logName)
        {
            byte[] args = new byte[4 + Params.Length];
            BitConverter.GetBytes(DeviceID).CopyTo(args, 0);
            Params.CopyTo(args, 4);

            byte[] status = CallMethod(DEVS, args);
            int result = BitConverter.ToInt32(status, 0);

            return BitConverter.ToInt32(status, 0);
        }

        public static int DeviceGet(uint DeviceID)
        {
            byte[] args = new byte[8];
            BitConverter.GetBytes(DeviceID).CopyTo(args, 0);
            byte[] status = CallMethod(DSTS, args);

            return BitConverter.ToInt32(status, 0) - 65536;
        }

        public static byte[] DeviceGetBuffer(uint DeviceID, uint Status = 0)
        {
            byte[] args = new byte[8];
            BitConverter.GetBytes(DeviceID).CopyTo(args, 0);
            BitConverter.GetBytes(Status).CopyTo(args, 4);

            return CallMethod(DSTS, args);
        }

        public static bool SetGPUEco(bool eco)
        {
            int currentState = DeviceGet(GPUEco);
            if (currentState < 0) return false; // Failed to retrieve state

            return (currentState == (eco ? 1 : 0)) || DeviceSet(GPUEco, eco ? 1 : 0) == 0;
        }

        public static bool SetXGMode(bool enable)
        {
            int currentState = DeviceGet(GPUXG);
            if (currentState < 0) return false; // Failed to retrieve state

            return (currentState == (enable ? 1 : 0)) || DeviceSet(GPUXG, enable ? 1 : 0) == 0;
        }

        public static int SetFanRange(AsusFan device, byte[] curve)
        {
            byte min = (byte)(curve[8] * 255 / 100);
            byte max = (byte)(curve[15] * 255 / 100);
            byte[] range = { min, max };

            int result;
            switch (device)
            {
                case AsusFan.GPU:
                    result = DeviceSet(DevsGPUFan, range, "FanRangeGPU");
                    break;
                default:
                    result = DeviceSet(DevsCPUFan, range, "FanRangeCPU");
                    break;
            }
            return result;
        }

        public static int SetFanSpeed(AsusFan device, byte speed)
        {
            byte[] curve = new byte[] { 0x1E, 0x28, 0x32, 0x3C, 0x46, 0x50, 0x5A, 0x5A, speed, speed, speed, speed, speed, speed, speed, speed };
            int result;
            switch (device)
            {
                case AsusFan.GPU:
                    result = DeviceSet(DevsGPUFanCurve, curve, "FanGPU");
                    break;
                case AsusFan.Mid:
                    result = DeviceSet(DevsMidFanCurve, curve, "FanMid");
                    break;
                default:
                    result = DeviceSet(DevsCPUFanCurve, curve, "FanCPU");
                    break;
            }
            return result;
        }

        public static int SetFanCurve(AsusFan device, byte[] curve)
        {
            if (curve.Length != 16)
                return -1;

            int result;
            int fanScale = 100;

            // if (fanScale != 100 && device == AsusFan.CPU) Logger.WriteLine("Custom fan scale: " + fanScale);
            // it seems to be a bug, when some old model's bios can go nuts if fan is set to 100% 
            for (int i = 8; i < curve.Length; i++)
                curve[i] = (byte)(Math.Max((byte)0, Math.Min((byte)99, curve[i])) * fanScale / 100);

            switch (device)
            {
                case AsusFan.GPU:
                    result = DeviceSet(DevsGPUFanCurve, curve, "FanGPU");
                    break;
                case AsusFan.Mid:
                    result = DeviceSet(DevsMidFanCurve, curve, "FanMid");
                    break;
                default:
                    result = DeviceSet(DevsCPUFanCurve, curve, "FanCPU");
                    break;
            }

            return result;
        }

        public static byte[] GetFanCurve(AsusFan device, int mode = 0)
        {
            uint fan_mode;

            // because it's asus, and modes are swapped here
            switch (mode)
            {
                case 1: fan_mode = 2; break;
                case 2: fan_mode = 1; break;
                default: fan_mode = 0; break;
            }

            switch (device)
            {
                case AsusFan.GPU:
                    return DeviceGetBuffer(DevsGPUFanCurve, fan_mode);
                case AsusFan.Mid:
                    return DeviceGetBuffer(DevsMidFanCurve, fan_mode);
                default:
                    return DeviceGetBuffer(DevsCPUFanCurve, fan_mode);
            }
        }

        public static bool IsInvalidCurve(byte[] curve)
        {
            return curve.Length != 16 || IsEmptyCurve(curve);
        }

        public static bool IsEmptyCurve(byte[] curve)
        {
            return false;
        }

        public static byte[] FixFanCurve(byte[] curve)
        {
            return curve;
        }

        public static bool IsXGConnected()
        {
            return DeviceGet(GPUXGConnected) == 1;
        }
    }
}
