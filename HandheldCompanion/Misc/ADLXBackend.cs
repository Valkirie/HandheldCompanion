using System.Runtime.InteropServices;

namespace HandheldCompanion.Misc
{
    public class ADLXBackend
    {
        public const string CppFunctionsDLL = @"PerformanceMetrics.dll";
        public const string CppFunctionsDLL1 = @"GraphSettings.dll";
        public const string CppFunctionsDLL2 = @"ADLX_DisplaySettings.dll";
        [DllImport(CppFunctionsDLL, CallingConvention = CallingConvention.Cdecl)] public static extern int GetFPSData();
        [DllImport(CppFunctionsDLL, CallingConvention = CallingConvention.Cdecl)] public static extern int GetGPUMetrics(int GPU, int Sensor);

        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetFPSLimit(int GPU, bool isEnabled, int FPS);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetAntiLag(int GPU, bool isEnabled);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetBoost(int GPU, bool isEnabled, int percent);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetRSR(bool isEnabled);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int GetRSRState();
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetRSRSharpness(int sharpness);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int GetRSRSharpness();
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetChill(int GPU, bool isEnabled, int maxFPS, int minFPS);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetImageSharpning(int GPU, bool isEnabled, int percent);
        [DllImport(CppFunctionsDLL1, CallingConvention = CallingConvention.Cdecl)] public static extern int SetEnhancedSync(int GPU, bool isEnabled);

        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasIntegerScalingSupport();
        // 0 is disabled, 1 is enabled
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern int SetIntegerScaling(int key);
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern bool IsIntegerScalingEnabled();
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasGPUScalingSupport();
        // 0 is disabled, 1 is enabled
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern int SetGPUScaling(int key);
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern bool IsGPUScalingEnabled();
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasScalingModeSupport();
        // scaling mode: 0 is preserve aspect ration, 1 is full panel, 2 is center
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern int SetScalingMode(int key);
        [DllImport(CppFunctionsDLL2, CallingConvention = CallingConvention.Cdecl)] public static extern int GetScalingMode();        
    }
}
