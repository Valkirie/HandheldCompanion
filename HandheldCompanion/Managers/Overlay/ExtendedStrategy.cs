using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.OSDStrategy.Overlay;

public class ExtendedStrategy(GPU gpu): IOverlayStrategy
{
    public string GetConfig()
    {
        OverlayRow row1 = new();
        OverlayEntry FPSentry = new("<APP>", "FF0000");
        FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
        FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
        row1.entries.Add(FPSentry);

        OverlayEntry GPUentry = new("GPU", "8040");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", "8000FF");
        OSDManager.AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), "GB");
        row1.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", "80FF");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
        row1.entries.Add(CPUentry);
        OverlayEntry RAMentry = new("RAM", "FF80C0");
        OSDManager.AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), "GB");
        row1.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", "FF8000");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
        row1.entries.Add(BATTentry);

        return row1.ToString();
    }
}
