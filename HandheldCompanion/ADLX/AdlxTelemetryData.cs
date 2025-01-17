using System.Runtime.InteropServices;

namespace HandheldCompanion.ADLX
{
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

        // Framerate
        public long timeStamp;
        public int fpsData;
    }
}
