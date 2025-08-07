using System;
using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.OSDStrategy.Overlay;

public class CustomStrategy(GPU gpu): IOverlayStrategy
{
    public string? GetConfig()
    {
        List<string> Content = [];
        for (int i = 0; i < OSDManager.OverlayCount; i++)
        {
            var name = OSDManager.OverlayOrder[i];
            var content = EntryContent(name, gpu);
            if (content == "") continue;
            Content.Add(content);
        }

        return Content.ToString();
    }
    
    
    public static string EntryContent(string name, GPU gpu)
    {
        OverlayRow row = new();
        OverlayEntry entry = new(name, EntryColor(name), true);
        switch (name.ToUpper())
        {
            case "TIME":
                switch (OSDManager.OverlayTimeLevel)
                {
                    case 2:
                    case 1:
                        entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString(), ""));
                        break;
                }
                break;
            case "FPS":
                switch (OSDManager.OverlayFPSLevel)
                {
                    case 2:
                        entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                        entry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                        break;
                    case 1:
                        entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                        break;
                }
                break;
            case "CPU":
                switch (OSDManager.OverlayCPULevel)
                {
                    case 2:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUTemperature(), "C");
                        break;
                    case 1:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
                        break;
                }
                break;
            case "RAM":
                switch (OSDManager.OverlayRAMLevel)
                {
                    case 2:
                    case 1:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), "GB");
                        break;
                }
                break;
            case "GPU":
                switch (OSDManager.OverlayGPULevel)
                {
                    case 2:
                        OSDManager.AddElementIfNotNull(entry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
                        OSDManager.AddElementIfNotNull(entry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
                        OSDManager.AddElementIfNotNull(entry, gpu.HasTemperature() ? gpu.GetTemperature() : PlatformManager.LibreHardwareMonitor.GetGPUTemperature(), "C");
                        break;
                    case 1:
                        OSDManager.AddElementIfNotNull(entry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
                        OSDManager.AddElementIfNotNull(entry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
                        break;
                }
                break;
            case "VRAM":
                switch (OSDManager.OverlayVRAMLevel)
                {
                    case 1:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), "GB");
                        break;
                    case 2:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), PlatformManager.LibreHardwareMonitor.GetGPUMemoryTotal(), "GB");
                        break;
                }
                break;
            case "BATT":
                switch (OSDManager.OverlayBATTLevel)
                {
                    case 2:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                        break;
                    case 1:
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                        OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                        break;
                }
                break;
        }

        // Skip empty rows
        if (entry.elements.Count == 0) return "";
        row.entries.Add(entry);
        return row.ToString();
    }

    private static string EntryColor(string name)
    {
        return name.ToUpper() switch
        {
            "FPS" => "FF0000",
            "CPU" => "80FF",
            "GPU" => "8040",
            "RAM" => "FF80C0",
            "VRAM" => "FF80FF",
            "BATT" => "FF8000",
            _ => "FFFFFF"
        };
    }
}