using System.Runtime.InteropServices;

public static class JSL
{
    public const int ButtonMaskUp = 0;
    public const int ButtonMaskDown = 1;
    public const int ButtonMaskLeft = 2;
    public const int ButtonMaskRight = 3;
    public const int ButtonMaskPlus = 4;
    public const int ButtonMaskOptions = 4;
    public const int ButtonMaskMinus = 5;
    public const int ButtonMaskShare = 5;
    public const int ButtonMaskLClick = 6;
    public const int ButtonMaskRClick = 7;
    public const int ButtonMaskL = 8;
    public const int ButtonMaskR = 9;
    public const int ButtonMaskZL = 10;
    public const int ButtonMaskZR = 11;
    public const int ButtonMaskS = 12;
    public const int ButtonMaskE = 13;
    public const int ButtonMaskW = 14;
    public const int ButtonMaskN = 15;
    public const int ButtonMaskHome = 16;
    public const int ButtonMaskPS = 16;
    public const int ButtonMaskCapture = 17;
    public const int ButtonMaskTouchpadClick = 17;
    public const int ButtonMaskSL = 18;
    public const int ButtonMaskSR = 19;

    [StructLayout(LayoutKind.Sequential)]
    public struct JOY_SHOCK_STATE
    {
        public int buttons;
        public float lTrigger;
        public float rTrigger;
        public float stickLX;
        public float stickLY;
        public float stickRX;
        public float stickRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMU_STATE
    {
        public float accelX;
        public float accelY;
        public float accelZ;
        public float gyroX;
        public float gyroY;
        public float gyroZ;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public unsafe struct JOY_SETTINGS
    {
        public int gyroSpace;
        public int colour;
        public int playerNumber;
        public int controllerType;
        public int splitType;
        public bool isCalibrating;
        public bool autoCalibrationEnabled;
        public bool isConnected;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string path;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOUCH_STATE
    {
        public int t0Id;
        public int t1Id;
        public bool t0Down;
        public bool t1Down;
        public float t0X;
        public float t0Y;
        public float t1X;
        public float t1Y;
    }

    public enum JOY_TYPE
    {
        Unknown,
        JoyConL,
        JoyConR,
        ProController,
        DualShock4,
        DualSense
    };

    public delegate void EventCallback(int handle, JOY_SHOCK_STATE state, JOY_SHOCK_STATE lastState,
        IMU_STATE imuState, IMU_STATE lastImuState, float deltaTime);

    [DllImport("JoyShockLibrary")]
    public static extern int JslConnectDevices();
    [DllImport("JoyShockLibrary")]
    public static extern int JslGetConnectedDeviceHandles(int[] deviceHandleArray, int size);
    [DllImport("JoyShockLibrary")]
    public static extern void JslDisconnectAndDisposeAll();
    [DllImport("JoyShockLibrary")]
    public static extern bool JslStillConnected(int deviceId);

    [DllImport("JoyShockLibrary", CallingConvention = CallingConvention.Cdecl)]
    public static extern JOY_SHOCK_STATE JslGetSimpleState(int deviceId);
    [DllImport("JoyShockLibrary", CallingConvention = CallingConvention.Cdecl)]
    public static extern IMU_STATE JslGetIMUState(int deviceId);
    [DllImport("JoyShockLibrary", CallingConvention = CallingConvention.Cdecl)]
    public static extern TOUCH_STATE JslGetTouchState(int deviceId, bool previous);

    [DllImport("JoyShockLibrary")]
    public static extern float JslGetStickStep(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern float JslGetTriggerStep(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern float JslGetPollRate(int deviceId);

    [DllImport("JoyShockLibrary")]
    public static extern void JslResetContinuousCalibration(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern void JslStartContinuousCalibration(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern void JslPauseContinuousCalibration(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern void JslGetCalibrationOffset(int deviceId, ref float xOffset, ref float yOffset, ref float zOffset);
    [DllImport("JoyShockLibrary")]
    public static extern void JslGetCalibrationOffset(int deviceId, float xOffset, float yOffset, float zOffset);

    [DllImport("JoyShockLibrary")]
    public static extern void JslSetCallback(EventCallback callback);
    
    [DllImport("JoyShockLibrary")]
    public static extern int JslGetControllerType(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern int JslGetControllerSplitType(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern int JslGetControllerColour(int deviceId);
    [DllImport("JoyShockLibrary")]
    public static extern void JslSetLightColour(int deviceId, int colour);
    [DllImport("JoyShockLibrary")]
    public static extern void JslSetRumble(int deviceId, int smallRumble, int bigRumble);
    [DllImport("JoyShockLibrary")]
    public static extern void JslSetPlayerNumber(int deviceId, int number);

    [DllImport("JoyShockLibrary")]
    public static extern JOY_SETTINGS JslGetControllerInfoAndSettings(int deviceId);

    // get touchpad
    [DllImport("JoyShockLibrary")]
    public static extern int JslGetTouchId(int deviceId, bool secondTouch = false);
    [DllImport("JoyShockLibrary")]
    public static extern bool JslGetTouchDown(int deviceId, bool secondTouch = false);

    [DllImport("JoyShockLibrary")]
    public static extern float JslGetTouchX(int deviceId, bool secondTouch = false);
    [DllImport("JoyShockLibrary")]
    public static extern float JslGetTouchY(int deviceId, bool secondTouch = false);

    [DllImport("JoyShockLibrary")]
    public static extern void JslSetAutomaticCalibration(int deviceId, bool enabled);
}