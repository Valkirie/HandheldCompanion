using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.Overlay.Strategy;


public class FullStrategy(GPU gpu): IOverlayStrategy
{
    public string? GetConfig()
    {
        OverlayRow row1 = new(); // GPU
        OverlayRow row2 = new(); // VRAM  
        OverlayRow row3 = new(); // CPU
        OverlayRow row4 = new(); // RAM
        OverlayRow row5 = new(); // Battery
        OverlayRow row6 = new(); // FPS

        OverlayEntry GPUentry = new("GPU", OverlayColors.GPU_COLOR, true);
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasTemperature() ? gpu.GetTemperature() : PlatformManager.LibreHardwareMonitor.GetGPUTemperature(), "C");
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", OverlayColors.VRAM_COLOR, true);
        OSDManager.AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), PlatformManager.LibreHardwareMonitor.GetGPUMemoryTotal(), "GB");
        row4.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", OverlayColors.CPU_COLOR, true);
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUTemperature(), "C");
        row2.entries.Add(CPUentry);

        OverlayEntry RAMentry = new("RAM", OverlayColors.RAM_COLOR, true);
        OSDManager.AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), PlatformManager.LibreHardwareMonitor.GetMemoryTotal(), "GB");
        row3.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", OverlayColors.BATT_COLOR, true);
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
        row5.entries.Add(BATTentry);

        OverlayEntry FPSentry = new("<APP>", OverlayColors.FPS_COLOR, true);
        FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
        FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
        row6.entries.Add(FPSentry);

        return string.Join("\n",
            row1.ToString(),
            row2.ToString(),
            row3.ToString(),
            row4.ToString(),
            row5.ToString(),
            row6.ToString()
        );
    }
}