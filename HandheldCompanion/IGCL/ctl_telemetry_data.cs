using System.Runtime.InteropServices;

namespace HandheldCompanion.IGCL
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ctl_telemetry_data
    {
        // GPU TDP
        public bool GpuEnergySupported;
        public double GpuEnergyValue;

        // GPU Voltage
        public bool GpuVoltageSupported;
        public double GpuVoltagValue; // Note: Typo in the original C++ code, should be "GpuVoltageValue" instead of "GpuVoltagValue"

        // GPU Core Frequency
        public bool GpuCurrentClockFrequencySupported;
        public double GpuCurrentClockFrequencyValue;

        // GPU Core Temperature
        public bool GpuCurrentTemperatureSupported;
        public double GpuCurrentTemperatureValue;

        // GPU Usage
        public bool GlobalActivitySupported;
        public double GlobalActivityValue;

        // Render Engine Usage
        public bool RenderComputeActivitySupported;
        public double RenderComputeActivityValue;

        // Media Engine Usage
        public bool MediaActivitySupported;
        public double MediaActivityValue;

        // VRAM Power Consumption
        public bool VramEnergySupported;
        public double VramEnergyValue;

        // VRAM Voltage
        public bool VramVoltageSupported;
        public double VramVoltageValue;

        // VRAM Frequency
        public bool VramCurrentClockFrequencySupported;
        public double VramCurrentClockFrequencyValue;

        // VRAM Read Bandwidth
        public bool VramReadBandwidthSupported;
        public double VramReadBandwidthValue;

        // VRAM Write Bandwidth
        public bool VramWriteBandwidthSupported;
        public double VramWriteBandwidthValue;

        // VRAM Temperature
        public bool VramCurrentTemperatureSupported;
        public double VramCurrentTemperatureValue;

        // Fanspeed (n Fans)
        public bool FanSpeedSupported;
        public double FanSpeedValue;
    }
}
