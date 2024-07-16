using System.Runtime.InteropServices;

namespace HandheldCompanion.Devices.Lenovo
{
    public class SapientiaUsb
    {
        private static readonly object _lock = new object();

        // Define the C extern functions and structures

        //leftGyroState rightGyroState 0 关闭陀螺仪 1开启陀螺仪
        public delegate void GyroStateCbFunc(int leftGyroState, int rightGyroState);
        //陀螺仪数据回调函数 leftGyroX左陀螺仪X leftGyroY 左陀螺仪Y rightGyroX 右陀螺仪X rightGyroY 右陀螺仪Y
        public delegate void GyroDataBackFunc(int leftGyroX, int leftGyroY, int rightGyroX, int rightGyroY);

        // 陀螺仪传感器状态
        [StructLayout(LayoutKind.Sequential)]
        public struct GyroSensorStatus
        {
            public uint gyro_timestamp;
            public int g_sensor_ax;
            public int g_sensor_ay;
            public int g_sensor_az;
            public int g_sensor_gx;
            public int g_sensor_gy;
            public int g_sensor_gz;
        };

        public delegate void GyroSensorStatusCbFunc(GyroSensorStatus left_gyro, GyroSensorStatus right_gyro);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "Init")]
        private static extern void InitInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "FreeSapientiaUsb")]
        private static extern void FreeSapientiaUsbInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLastErr")]
        private static extern int GetLastErrInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGamePadMode")]
        private static extern bool SetGamePadModeInternal(int modeType);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetGamePadMode")]
        private static extern int GetGamePadModeInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLeftGyroStatus")]
        private static extern int GetLeftGyroStatusInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetLeftGyroStatus")]
        private static extern bool SetLeftGyroStatusInternal(int status);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetRightGyroStatus")]
        private static extern int GetRightGyroStatusInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetRightGyroStatus")]
        private static extern bool SetRightGyroStatusInternal(int status);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroState")]
        private static extern bool SetGyroStateInternal(int iGyro, int value);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroStateCbFunc")]
        private static extern bool SetGyroStateCbFuncInternal(GyroStateCbFunc cbFunc);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetGyroMode")]
        private static extern int GetGyroModeInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetGyroModeStatus")]
        private static extern int GetGyroModeStatusInternal(int mode, int gyroIndex);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroModeStatus")]
        private static extern int SetGyroModeStatusInternal(int mode, int gyroIndex, int gyroStatus);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroDataBackFunc")]
        private static extern bool SetGyroDataBackFuncInternal(GyroDataBackFunc BackFunc);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroSensorStatusBackFunc")]
        private static extern bool SetGyroSensorStatusBackFuncInternal(GyroSensorStatusCbFunc BackFunc);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLlightingEffectEnable")]
        private static extern int GetLlightingEffectEnableInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetLightingEnable")]
        private static extern bool SetLightingEnableInternal(int device, bool iswitch);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetCurrentLightProfile")]
        private static extern LightionProfile GetCurrentLightProfileInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetLightingEffectProfileID")]
        private static extern bool SetLightingEffectProfileIDInternal(int device, LightionProfile lightPro);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetQuickLightingEffect")]
        private static extern int GetQuickLightingEffectInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetQuickLightingEffectEnable")]
        private static extern int GetQuickLightingEffectEnableInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetQuickLightingEffectEnable")]
        private static extern bool SetQuickLightingEffectEnableInternal(int device, bool enable);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetQuickLightingEffect")]
        private static extern bool SetQuickLightingEffectInternal(int device, int index);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetTouchPadStatus")]
        private static extern int GetTouchPadStatusInternal();

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetTrackpadStatus")]
        private static extern int GetTrackpadStatusInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetTouchPadStatus")]
        private static extern bool SetTouchPadStatusInternal(int iSwitch);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetDeviceDefault")]
        private static extern bool SetDeviceDefaultInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "getUSBVerify")]
        private static extern VERSION getUSBVerifyInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetStickCustomCurve")]
        private static extern LegionJoystickCurveProfile GetStickCustomCurveInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetStickCustomCurve")]
        private static extern bool SetStickCustomCurveInternal(int device, LegionJoystickCurveProfile curveProfile);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetStickCustomDeadzone")]
        private static extern int GetStickCustomDeadzoneInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetStickCustomDeadzone")]
        private static extern bool SetStickCustomDeadzoneInternal(int device, int deadzone);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetGyroSensorDataOnorOff")]
        private static extern bool SetGyroSensorDataOnorOffInternal(int device, int status);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetTriggerDeadzoneAndMargin")]
        private static extern LegionTriggerDeadzone GetTriggerDeadzoneAndMarginInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetTriggerDeadzoneAndMargin")]
        private static extern bool SetTriggerDeadzoneAndMarginInternal(int device, LegionTriggerDeadzone deadzone);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetAutoSleepTime")]
        private static extern int GetAutoSleepTimeInternal(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "SetAutoSleepTime")]
        private static extern bool SetAutoSleepTimeInternal(int device, int autosleeptime);

        // Thread-safe wrapper methods

        public static void Init()
        {
            lock (_lock)
            {
                InitInternal();
            }
        }

        public static void FreeSapientiaUsb()
        {
            lock (_lock)
            {
                FreeSapientiaUsbInternal();
            }
        }

        public static int GetLastErr()
        {
            lock (_lock)
            {
                return GetLastErrInternal();
            }
        }

        public static bool SetGamePadMode(int modeType)
        {
            lock (_lock)
            {
                return SetGamePadModeInternal(modeType);
            }
        }

        public static int GetGamePadMode()
        {
            lock (_lock)
            {
                return GetGamePadModeInternal();
            }
        }

        public static int GetLeftGyroStatus()
        {
            lock (_lock)
            {
                return GetLeftGyroStatusInternal();
            }
        }

        public static bool SetLeftGyroStatus(int status)
        {
            lock (_lock)
            {
                return SetLeftGyroStatusInternal(status);
            }
        }

        public static int GetRightGyroStatus()
        {
            lock (_lock)
            {
                return GetRightGyroStatusInternal();
            }
        }

        public static bool SetRightGyroStatus(int status)
        {
            lock (_lock)
            {
                return SetRightGyroStatusInternal(status);
            }
        }

        public static bool SetGyroState(int iGyro, int value)
        {
            lock (_lock)
            {
                return SetGyroStateInternal(iGyro, value);
            }
        }

        public static bool SetGyroStateCbFunc(GyroStateCbFunc cbFunc)
        {
            lock (_lock)
            {
                return SetGyroStateCbFuncInternal(cbFunc);
            }
        }

        public static int GetGyroMode()
        {
            lock (_lock)
            {
                return GetGyroModeInternal();
            }
        }

        public static int GetGyroModeStatus(int mode, int gyroIndex)
        {
            lock (_lock)
            {
                return GetGyroModeStatusInternal(mode, gyroIndex);
            }
        }

        public static int SetGyroModeStatus(int mode, int gyroIndex, int gyroStatus)
        {
            lock (_lock)
            {
                return SetGyroModeStatusInternal(mode, gyroIndex, gyroStatus);
            }
        }

        public static bool SetGyroDataBackFunc(GyroDataBackFunc BackFunc)
        {
            lock (_lock)
            {
                return SetGyroDataBackFuncInternal(BackFunc);
            }
        }

        public static bool SetGyroSensorStatusBackFunc(GyroSensorStatusCbFunc BackFunc)
        {
            lock (_lock)
            {
                return SetGyroSensorStatusBackFuncInternal(BackFunc);
            }
        }

        public static int GetLlightingEffectEnable(int device)
        {
            lock (_lock)
            {
                return GetLlightingEffectEnableInternal(device);
            }
        }

        public static bool SetLightingEnable(int device, bool iswitch)
        {
            lock (_lock)
            {
                return SetLightingEnableInternal(device, iswitch);
            }
        }

        public static LightionProfile GetCurrentLightProfile(int device)
        {
            lock (_lock)
            {
                return GetCurrentLightProfileInternal(device);
            }
        }

        public static bool SetLightingEffectProfileID(int device, LightionProfile lightPro)
        {
            lock (_lock)
            {
                return SetLightingEffectProfileIDInternal(device, lightPro);
            }
        }

        public static int GetQuickLightingEffect(int device)
        {
            lock (_lock)
            {
                return GetQuickLightingEffectInternal(device);
            }
        }

        public static int GetQuickLightingEffectEnable(int device)
        {
            lock (_lock)
            {
                return GetQuickLightingEffectEnableInternal(device);
            }
        }

        public static bool SetQuickLightingEffectEnable(int device, bool enable)
        {
            lock (_lock)
            {
                return SetQuickLightingEffectEnableInternal(device, enable);
            }
        }

        public static bool SetQuickLightingEffect(int device, int index)
        {
            lock (_lock)
            {
                return SetQuickLightingEffectInternal(device, index);
            }
        }

        public static int GetTouchPadStatus()
        {
            lock (_lock)
            {
                return GetTouchPadStatusInternal();
            }
        }

        public static int GetTrackpadStatus(int device)
        {
            lock (_lock)
            {
                return GetTrackpadStatusInternal(device);
            }
        }

        public static bool SetTouchPadStatus(int iSwitch)
        {
            lock (_lock)
            {
                return SetTouchPadStatusInternal(iSwitch);
            }
        }

        public static bool SetDeviceDefault(int device)
        {
            lock (_lock)
            {
                return SetDeviceDefaultInternal(device);
            }
        }

        public static VERSION getUSBVerify(int device)
        {
            lock (_lock)
            {
                return getUSBVerifyInternal(device);
            }
        }

        public static LegionJoystickCurveProfile GetStickCustomCurve(int device)
        {
            lock (_lock)
            {
                return GetStickCustomCurveInternal(device);
            }
        }

        public static bool SetStickCustomCurve(int device, LegionJoystickCurveProfile curveProfile)
        {
            lock (_lock)
            {
                return SetStickCustomCurveInternal(device, curveProfile);
            }
        }

        public static int GetStickCustomDeadzone(int device)
        {
            lock (_lock)
            {
                return GetStickCustomDeadzoneInternal(device);
            }
        }

        public static bool SetStickCustomDeadzone(int device, int deadzone)
        {
            lock (_lock)
            {
                return SetStickCustomDeadzoneInternal(device, deadzone);
            }
        }

        public static bool SetGyroSensorDataOnorOff(int device, int status)
        {
            lock (_lock)
            {
                return SetGyroSensorDataOnorOffInternal(device, status);
            }
        }

        public static LegionTriggerDeadzone GetTriggerDeadzoneAndMargin(int device)
        {
            lock (_lock)
            {
                return GetTriggerDeadzoneAndMarginInternal(device);
            }
        }

        public static bool SetTriggerDeadzoneAndMargin(int device, LegionTriggerDeadzone deadzone)
        {
            lock (_lock)
            {
                return SetTriggerDeadzoneAndMarginInternal(device, deadzone);
            }
        }

        public static int GetAutoSleepTime(int device)
        {
            lock (_lock)
            {
                return GetAutoSleepTimeInternal(device);
            }
        }

        public static bool SetAutoSleepTime(int device, int autosleeptime)
        {
            lock (_lock)
            {
                return SetAutoSleepTimeInternal(device, autosleeptime);
            }
        }

        //导出类
        [StructLayout(LayoutKind.Sequential)]
        public struct LightionProfile
        {
            public int effect;
            public int r;
            public int g;
            public int b;
            public int brightness;
            public int speed;
            public int profile;
            public LightionProfile()
            {
                effect = 0;
                r = 0;
                g = 0;
                b = 0;
                brightness = 0;
                speed = 0;
                profile = 0;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct LegionJoystickCurveProfile
        {
            public int X1;
            public int X2;
            public int Y1;
            public int Y2;
            public LegionJoystickCurveProfile()
            {
                X1 = 60;
                Y1 = 60;
                X2 = 90;
                Y2 = 90;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct LegionTriggerDeadzone
        {
            public int Deadzone;
            public int Margin;
            public LegionTriggerDeadzone()
            {
                Deadzone = 5;
                Margin = 5;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct VERSION
        {
            public int verPro;
            public int verCMD;
            public int verFir;
            public int verHard;

            public VERSION(int verPro, int verCMD, int verFir, int verHard)
            {
                this.verPro = verPro;
                this.verCMD = verCMD;
                this.verFir = verFir;
                this.verHard = verHard;
            }
        }
    }
}
