using System.Runtime.InteropServices;

namespace HandheldCompanion.ADLX
{
    public class ADLXBackend
    {
        public const string ADLX_Wrapper = @"ADLX_Wrapper.dll";

        [StructLayout(LayoutKind.Sequential)]
        public struct AdlxTelemetryData
        {
            // GPU Usage
            public bool gpuUsageSupported;
            public double gpuUsageValue;

            // GPU Core Frequency
            public bool gpuClockSpeedSupported;
            public double gpuClockSpeedValue;

            // GPU VRAM Frequency
            public bool gpuVRAMClockSpeedSupported;
            public double gpuVRAMClockSpeedValue;

            // GPU Core Temperature
            public bool gpuTemperatureSupported;
            public double gpuTemperatureValue;

            // GPU Hotspot Temperature
            public bool gpuHotspotTemperatureSupported;
            public double gpuHotspotTemperatureValue;

            // GPU Power
            public bool gpuPowerSupported;
            public double gpuPowerValue;

            // Fan Speed
            public bool gpuFanSpeedSupported;
            public double gpuFanSpeedValue;

            // VRAM Usage
            public bool gpuVramSupported;
            public double gpuVramValue;

            // GPU Voltage
            public bool gpuVoltageSupported;
            public double gpuVoltageValue;

            // GPU TBP
            public bool gpuTotalBoardPowerSupported;
            public double gpuTotalBoardPowerValue;
        }

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool IntializeAdlx();
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool CloseAdlx();

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasRSRSupport();
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetRSR();
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetRSR(bool enable);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetRSRSharpness();
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetRSRSharpness(int sharpness);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetAntiLag(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetAntiLag(int GPU, bool enable);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetBoost(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetBoost(int GPU, bool enable);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetBoostResolution(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetBoostResolution(int GPU, int minRes);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetChill(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChill(int GPU, bool enable);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetChillMinFPS(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChillMinFPS(int GPU, int minFPS);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetChillMaxFPS(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetChillMaxFPS(int GPU, int maxFPS);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetImageSharpening(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetImageSharpening(int GPU, bool enable);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetImageSharpeningSharpness(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetImageSharpeningSharpness(int GPU, int sharpness);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasIntegerScalingSupport(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetIntegerScaling(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetIntegerScaling(int GPU, bool enabled);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasGPUScalingSupport(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetGPUScaling(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetGPUScaling(int GPU, bool enabled);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool HasScalingModeSupport(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern int GetScalingMode(int GPU);
        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool SetScalingMode(int GPU, int mode);

        [DllImport(ADLX_Wrapper, CallingConvention = CallingConvention.Cdecl)] public static extern bool GetAdlxTelemetry(int GPU, ref AdlxTelemetryData adlxTelemetryData);

        internal static AdlxTelemetryData GetTelemetryData()
        {
            AdlxTelemetryData TelemetryData = new();
            bool Result = GetAdlxTelemetry(0, ref TelemetryData);
            return TelemetryData;
        }
    }
}
