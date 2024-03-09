using System.Runtime.InteropServices;

namespace HandheldCompanion.Devices.Lenovo
{
    // Define the C extern functions and structures
    public class SapientiaUsb
    {
        //leftGyroState rightGyroState 0 关闭陀螺仪 1开启陀螺仪
        public delegate void GyroStateCbFunc(int leftGyroState, int rightGyroState);
        //陀螺仪数据回调函数 leftGyroX左陀螺仪X leftGyroY 左陀螺仪Y rightGyroX 右陀螺仪X rightGyroY 右陀螺仪Y
        public delegate void GyroDataBackFunc(int leftGyroX, int leftGyroY, int rightGyroX, int rightGyroY);

        // 陀螺仪传感器状态
        [StructLayout(LayoutKind.Sequential)]
        public struct GyroSensorStatus
        {
            /* 
                gyro_timestamp: 陀螺仪时间戳(0 - 255)
            */
            public uint gyro_timestamp;
            /*
                g_sensor_ax: 陀螺仪角速度 X 轴
            */
            public int g_sensor_ax;
            /*
                g_sensor_ay: 陀螺仪角速度 Y 轴        
            */
            public int g_sensor_ay;
            /*
                g_sensor_az: 陀螺仪角速度 Z 轴        
            */
            public int g_sensor_az;
            /*
                g_sensor_gx: 陀螺仪重力加速度 X 轴
            */
            public int g_sensor_gx;
            /*
               g_sensor_gy: 陀螺仪重力加速度 Y 轴
           */
            public int g_sensor_gy;
            /*
               g_sensor_gz: 陀螺仪重力加速度 Z 轴
           */
            public int g_sensor_gz;
        };

        // 陀螺仪传感器状态回调 left_gyro: 左陀螺仪传感器数据 right_gyro: 右陀螺仪传感器数据
        public delegate void GyroSensorStatusCbFunc(GyroSensorStatus left_gyro, GyroSensorStatus right_gyro);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void Init();

        //释放DLL线程和内存
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void FreeSapientiaUsb();

        //0.正常, 1.发送数据失败 2。接收数据失败 3.连接手柄失败
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetLastErr();

        //mode：1.XBOX 模式（默认）2.Nintendo 模式 XBOX 模式与 Nintendo 模式的区别仅 ABXY 按键布局不同
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetGamePadMode(int modeType);
        //获取手柄工作模式 mode：1.XBOX 模式（默认）2.Nintendo 模式
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetGamePadMode();
        //获取左陀螺仪状态  status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetLeftGyroStatus();
        //设置左陀螺仪状态  status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetLeftGyroStatus(int status);
        //获取右陀螺仪状态  status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetRightGyroStatus();
        //设置右陀螺仪状态  status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetRightGyroStatus(int status);
        //iGyro: 3：Gamepad_L，4：Gamepad_R      sSetLinkStateBackFunctatus：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetGyroState(int iGyro, int value);
        //设置陀螺仪回调函数
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetGyroStateCbFunc(GyroStateCbFunc cbFunc);
        //获取陀螺仪模式
        //return Mode：0：disable，1：attached，2：detached
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetGyroMode();
        //获取螺仪模式状态  Mode：0：disable，1：attached，2：detached  device: 1：RX，2：Dongle
        //return  GyroStatus：0：disable，1：As Left Joystick；2：As Right Joystick
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetGyroModeStatus(int mode, int gyroIndex);
        //设置螺仪模式状态  Mode：0：disable，1：attached; 2：detached 
        //  device: 1：RX，2：Dongle;
        //  GyroIndex：1：左陀螺仪，2：右陀螺仪;
        //  GyroStatus：0：disable，1：As Left Joystick；2：As Right Joystick
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int SetGyroModeStatus(int mode, int gyroIndex, int gyroStatus);
        //陀螺仪数据回调函数
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetGyroDataBackFunc(GyroDataBackFunc BackFunc);
        //设置陀螺仪传感器数据回调函数
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetGyroSensorStatusBackFunc(GyroSensorStatusCbFunc BackFunc);

        //获取灯效开关 1.开启 0.关闭  //device 3:左手柄 4:右手柄
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetLlightingEffectEnable(int device);
        //设置灯效开关 1.开启 0.关闭 //device 3:左手柄 4:右手柄
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetLightingEnable(int device, bool iswitch);
        // 获取当前灯效配置 //device  3:左手柄 4:右手柄
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern LightionProfile GetCurrentLightProfile(int device);
        //设置当前灯效配置 //device 3:左手柄 4:右手柄
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetLightingEffectProfileID(int device, LightionProfile lightPro);
        //获取灯效配置页 profile: 1：Lighting Profile 01;2：Lighting Profile 02;3：Lighting Profile 03
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetQuickLightingEffect(int device);
        //获取快捷灯效开关 return 1.开 0.关 -1.未获取成功
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetQuickLightingEffectEnable(int device);
        //快捷设置灯效开关 //device 3:左手柄 4:右手柄  //index:快捷设置灯效配置页 profile: 1：Lighting Profile 01;2：Lighting Profile 02;3：Lighting Profile 03
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetQuickLightingEffectEnable(int device, bool enable);
        //设置灯效配置页 device: 3为左手柄 4为右手柄 index 为 profile: 1：Lighting Profile 01; 2：Lighting Profile 02; 3：Lighting Profile 03
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetQuickLightingEffect(int device, int index);
        //获取触摸板状态 tatus：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetTouchPadStatus();
        //获取触摸板状态    status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetTrackpadStatus(int device);
        //设置触摸板状态 status：0:关，1:开
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetTouchPadStatus(int iSwitch);
        //恢复出厂设置 device: 1：RX，2：Dongle; 3:左手柄 4:右手柄
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetDeviceDefault(int device);
        //手柄版本信息
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern VERSION getUSBVerify(int device);

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

        // Below are converted by @MSeys

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern LegionJoystickCurveProfile GetStickCustomCurve(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetStickCustomCurve(int device, LegionJoystickCurveProfile curveProfile);

        // Deadzone Range is 0-99 (LS shows 1%-100%)
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetStickCustomDeadzone(int device);

        // Deadzone Range is 0-99 (LS shows 1%-100%)
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetStickCustomDeadzone(int device, int deadzone);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool GetGyroSensorDataOnorOff(int device);

        // Range is 0-99 on Deadzone and Margin
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern LegionTriggerDeadzone GetTriggerDeadzoneAndMargin(int device);

        // Range is 0-99 on Deadzone and Margin
        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetTriggerDeadzoneAndMargin(int device, LegionTriggerDeadzone deadzone);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int GetAutoSleepTime(int device);

        [DllImport("SapientiaUsb.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetAutoSleepTime(int device, int autosleeptime);

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
    }
}
