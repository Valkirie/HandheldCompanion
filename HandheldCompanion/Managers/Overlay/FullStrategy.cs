using System.Collections.Generic;

using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.Overlay;

public class FullStrategy(GPU gpu): IOverlayStrategy
{
    private const string GPU_COLOR = "8040";
    private const string VRAM_COLOR = "8000FF";
    private const string CPU_COLOR = "80FF";
    private const string RAM_COLOR = "FF80C0";
    private const string BATT_COLOR = "FF8000";
    private const string FPS_COLOR = "FF0000";

    public string? GetConfig()
    {
        List<string> Content = [];
        OverlayRow row1 = new();
        OverlayRow row2 = new();
        OverlayRow row3 = new();
        OverlayRow row4 = new();
        OverlayRow row5 = new();
        OverlayRow row6 = new();

        OverlayEntry GPUentry = new("GPU", GPU_COLOR, true);
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
        OSDManager.AddElementIfNotNull(GPUentry, gpu.HasTemperature() ? gpu.GetTemperature() : PlatformManager.LibreHardwareMonitor.GetGPUTemperature(), "C");
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", VRAM_COLOR, true);
        OSDManager.AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), PlatformManager.LibreHardwareMonitor.GetGPUMemoryTotal(), "GB");
        row4.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", CPU_COLOR, true);
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
        OSDManager.AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.GetCPUTemperature(), "C");
        row2.entries.Add(CPUentry);

        OverlayEntry RAMentry = new("RAM", RAM_COLOR, true);
        OSDManager.AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), PlatformManager.LibreHardwareMonitor.GetMemoryTotal(), "GB");
        row3.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", BATT_COLOR, true);
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
        OSDManager.AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
        row5.entries.Add(BATTentry);

        OverlayEntry FPSentry = new("<APP>", FPS_COLOR, true);
        FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
        FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
        row6.entries.Add(FPSentry);

        Content.Add(row1.ToString());
        Content.Add(row2.ToString());
        Content.Add(row3.ToString());
        Content.Add(row4.ToString());
        Content.Add(row5.ToString());
        Content.Add(row6.ToString());

        return string.Join("\n", Content);
    }
}