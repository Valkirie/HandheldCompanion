using System;
using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.Overlay.Strategy;

public class ExtendedStrategy(GPU gpu): IOverlayStrategy
{
    private readonly GPU _gpu = gpu ?? throw new ArgumentNullException(nameof(gpu));
    
    public string GetConfig()
    {
        OverlayRow row1 = new();
        OverlayEntry FPSentry = new("<APP>", OverlayColors.FPS_COLOR);
        FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
        FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
        row1.entries.Add(FPSentry);

        OverlayEntry GPUentry = new("GPU", OverlayColors.GPU_COLOR);
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", OverlayColors.VRAM_COLOR);
        OSDManager.AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), "GB");
        row1.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", OverlayColors.CPU_COLOR);
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
        row1.entries.Add(CPUentry);
        OverlayEntry RAMentry = new("RAM", OverlayColors.RAM_COLOR);
        OSDManager.AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), "GB");
        row1.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", OverlayColors.BATT_COLOR);
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
        row1.entries.Add(BATTentry);

        return row1.ToString();
    }
}
