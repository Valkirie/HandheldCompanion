using System.Runtime.InteropServices;

namespace HandheldCompanion.ADLX
{
    public class ADLXBackend
    {
        public const string ADLX_3DSettings = @"ADLX_3DSettings.dll";
        public const string ADLX_DisplaySettings = @"ADLX_DisplaySettings.dll";

        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasRSRSupport();
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetRSR();
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetRSR(bool enable);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetRSRSharpness();
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetRSRSharpness(int sharpness);

        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetAntiLag(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetAntiLag(int GPU, bool enable);

        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetBoost(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetBoost(int GPU, bool enable);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetBoostResolution(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetBoostResolution(int GPU, int minRes);

        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetChill(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChill(int GPU, bool enable);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetChillMinFPS(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChillMinFPS(int GPU, int minFPS);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetChillMaxFPS(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChillMaxFPS(int GPU, int maxFPS);

        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetImageSharpening(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetImageSharpening(int GPU, bool enable);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetImageSharpeningSharpness(int GPU);
        [DllImport(ADLX_3DSettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetImageSharpeningSharpness(int GPU, int sharpness);

        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasIntegerScalingSupport(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetIntegerScaling(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetIntegerScaling(int GPU, bool enabled);

        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasGPUScalingSupport(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetGPUScaling(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetGPUScaling(int GPU, bool enabled);

        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasScalingModeSupport(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern int GetScalingMode(int GPU);
        [DllImport(ADLX_DisplaySettings, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetScalingMode(int GPU, int mode);
    }
}
