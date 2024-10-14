using HandheldCompanion.GraphicsProcessingUnit;
using PrecisionTiming;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HandheldCompanion.Managers;

public static class OSDManager
{
    public delegate void InitializedEventHandler();

    // C1: GPU
    // C2: CPU
    // C3: RAM
    // C4: VRAM
    // C5: BATT
    // C6: FPS
    private const string Header =
        "<C0=FFFFFF><C1=8000FF><A0=-4><S0=-50><S1=50>";

    private static bool IsInitialized;
    public static string[] OverlayOrder;
    public static int OverlayCount;
    public static short OverlayLevel;
    public static short OverlayTimeLevel;
    public static short OverlayFPSLevel;
    public static short OverlayCPULevel;
    public static short OverlayRAMLevel;
    public static short OverlayGPULevel;
    public static short OverlayVRAMLevel;
    public static short OverlayBATTLevel;

    private static readonly PrecisionTimer RefreshTimer;
    private static int RefreshInterval = 100;

    private static readonly ConcurrentDictionary<int, OSD> OnScreenDisplay = new();
    private static AppEntry OnScreenAppEntry;
    private static List<string> Content;

    static OSDManager()
    {
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Hooked += RTSS_Hooked;
        PlatformManager.RTSS.Unhooked += RTSS_Unhooked;

        // timer used to monitor foreground application framerate
        RefreshInterval = SettingsManager.GetInt("OnScreenDisplayRefreshRate");

        // OverlayLevel
        OverlayLevel = Convert.ToInt16(SettingsManager.GetInt("OnScreenDisplayLevel"));

        RefreshTimer = new PrecisionTimer();
        RefreshTimer.SetInterval(new Action(UpdateOSD), RefreshInterval, false, 0, TimerMode.Periodic, true);
    }

    public static event InitializedEventHandler Initialized;

    private static void RTSS_Unhooked(int processId)
    {
        try
        {
            // clear previous display
            if (OnScreenDisplay.TryGetValue(processId, out var OSD))
            {
                OSD.Update("");
                OSD.Dispose();

                OnScreenDisplay.TryRemove(new KeyValuePair<int, OSD>(processId, OSD));
            }
        }
        catch
        {
        }
    }

    private static void RTSS_Hooked(AppEntry appEntry)
    {
        try
        {
            // update foreground id
            OnScreenAppEntry = appEntry;

            // only create a new OSD if needed
            if (OnScreenDisplay.ContainsKey(appEntry.ProcessId))
                return;

            OnScreenDisplay[OnScreenAppEntry.ProcessId] = new OSD(OnScreenAppEntry.Name);
        }
        catch
        {
        }
    }

    public static void Start()
    {
        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "OSDManager");

        if (OverlayLevel != 0 && !RefreshTimer.IsRunning())
            RefreshTimer.Start();
    }

    private static void UpdateOSD()
    {
        if (OverlayLevel == 0)
            return;

        foreach (var pair in OnScreenDisplay)
        {
            var processId = pair.Key;
            var processOSD = pair.Value;

            try
            {
                if (processId == OnScreenAppEntry.ProcessId)
                {
                    var content = Draw(processId);
                    processOSD.Update(content);
                }
                else
                {
                    processOSD.Update("");
                }
            }
            catch
            {
            }
        }
    }

    public static string Draw(int processId)
    {
        Content = [];
        GPU gpu = GPUManager.GetCurrent();
        if (gpu is null)
            goto Exit;

        switch (OverlayLevel)
        {
            default:
            case 0: // Disabled
                break;

            case 1: // Minimal
                {
                    OverlayRow row1 = new();

                    OverlayEntry FPSentry = new("<APP>", "FF0000");
                    FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                    FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                    row1.entries.Add(FPSentry);

                    // add header to row1
                    Content.Add(Header + row1);
                }
                break;

            case 2: // Extended
                {
                    OverlayRow row1 = new();
                    OverlayEntry FPSentry = new("<APP>", "FF0000");
                    FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                    FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                    row1.entries.Add(FPSentry);

                    OverlayEntry GPUentry = new("GPU", "8040");
                    AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : null, "%");
                    AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : null, "W");
                    row1.entries.Add(GPUentry);

                    OverlayEntry CPUentry = new("CPU", "80FF");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                    row1.entries.Add(CPUentry);
                    OverlayEntry RAMentry = new("RAM", "FF80C0");
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.MemoryUsage, "MiB");
                    row1.entries.Add(RAMentry);

                    OverlayEntry VRAMentry = new("VRAM", "8000FF");
                    AddElementIfNotNull(VRAMentry, gpu.GetVRAMUsage(), "MiB");
                    row1.entries.Add(VRAMentry);

                    OverlayEntry BATTentry = new("BATT", "FF8000");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                    row1.entries.Add(BATTentry);

                    // add header to row1
                    Content.Add(Header + row1);
                }
                break;

            case 3: // Full
                {
                    OverlayRow row1 = new();
                    OverlayRow row2 = new();
                    OverlayRow row3 = new();
                    OverlayRow row4 = new();
                    OverlayRow row5 = new();
                    OverlayRow row6 = new();

                    OverlayEntry GPUentry = new("GPU", "8040", true);
                    AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : null, "%");
                    AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : null, "W");
                    AddElementIfNotNull(GPUentry, gpu.HasTemperature() ? gpu.GetTemperature() : null, "C");
                    row1.entries.Add(GPUentry);

                    OverlayEntry CPUentry = new("CPU", "80FF", true);
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUTemperatur, "C");
                    row2.entries.Add(CPUentry);

                    OverlayEntry VRAMentry = new("VRAM", "8000FF", true);
                    AddElementIfNotNull(VRAMentry, gpu.GetVRAMUsage(), "MiB");
                    row4.entries.Add(VRAMentry);

                    OverlayEntry RAMentry = new("RAM", "FF80C0", true);
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.MemoryUsage, "MiB");
                    row3.entries.Add(RAMentry);

                    OverlayEntry BATTentry = new("BATT", "FF8000", true);
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryPower, "W");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                    row5.entries.Add(BATTentry);

                    OverlayEntry FPSentry = new("<APP>", "FF0000", true);
                    FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                    FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                    row6.entries.Add(FPSentry);

                    // add header to row1
                    Content.Add(Header + row1);
                    Content.Add(row2.ToString());
                    Content.Add(row3.ToString());
                    Content.Add(row4.ToString());
                    Content.Add(row5.ToString());
                    Content.Add(row6.ToString());
                }
                break;

            case 4:
                {
                    for (int i = 0; i < OverlayCount; i++)
                    {
                        var name = OverlayOrder[i];
                        var content = EntryContent(name, gpu);
                        if (content == "") continue;
                        Content.Add(content);
                    }

                    // Add header to row1
                    if (Content.Count > 0) Content[0] = Header + Content[0];
                }
                break;

            case 5: // External
                {
                    /*
                     * Intended to simply allow RTSS/HWINFO to run, and let the user configure the overlay within those
                     * tools as they wish
                     */
                }
                break;
        }

    Exit:
        return string.Join("\n", Content);
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        RefreshTimer.Stop();

        // unhook all processes
        foreach (var processId in OnScreenDisplay.Keys)
            RTSS_Unhooked(processId);

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "OSDManager");
    }

    private static string EntryContent(String name, GPU gpu)
    {
        OverlayRow row = new();
        OverlayEntry entry = new(name, EntryColor(name), true);
        switch (name.ToUpper())
        {
            case "TIME":
                switch (OverlayTimeLevel)
                {
                    case 2:
                    case 1:
                        entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString(), ""));
                        break;
                }
                break;
            case "FPS":
                switch (OverlayFPSLevel)
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
                switch (OverlayCPULevel)
                {
                    case 2:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.CPUTemperatur, "C");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                        break;
                }
                break;
            case "RAM":
                switch (OverlayRAMLevel)
                {
                    case 2:
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.MemoryUsage, "MiB");
                        break;
                }
                break;
            case "GPU":
                switch (OverlayGPULevel)
                {
                    case 2:
                        AddElementIfNotNull(entry, gpu.GetLoad(), "%");
                        AddElementIfNotNull(entry, gpu.GetPower(), "W");
                        AddElementIfNotNull(entry, gpu.GetTemperature(), "C");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, gpu.GetLoad(), "%");
                        AddElementIfNotNull(entry, gpu.GetPower(), "W");
                        break;
                }
                break;
            case "VRAM":
                switch (OverlayVRAMLevel)
                {
                    case 2:
                    case 1:
                        AddElementIfNotNull(entry, gpu.GetVRAMUsage(), "MiB");
                        break;
                }
                break;
            case "BATT":
                switch (OverlayBATTLevel)
                {
                    case 2:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.BatteryPower, "W");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                        break;
                }
                break;
        }
        // Skip empty rows
        if (entry.elements.Count == 0) return "";
        row.entries.Add(entry);
        return row.ToString();
    }

    private static string EntryColor(String name)
    {
        switch (name.ToUpper())
        {
            case "FPS":
                return "FF0000";
            case "CPU":
                return "80FF";
            case "GPU":
                return "8040";
            case "RAM":
                return "FF80C0";
            case "VRAM":
                return "FF80FF";
            case "BATT":
                return "FF8000";
            default:
                return "FFFFFF";
        }
    }

    private static void AddElementIfNotNull(OverlayEntry entry, float? value, String unit)
    {
        if (value is not null)
        {
            entry.elements.Add(new OverlayEntryElement((float)value, unit));
        }
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "OnScreenDisplayRefreshRate":
                {
                    RefreshInterval = Convert.ToInt32(value);

                    if (RefreshTimer.IsRunning())
                    {
                        RefreshTimer.Stop();
                        RefreshTimer.SetPeriod(RefreshInterval);
                        RefreshTimer.Start();
                    }
                }
                break;

            case "OnScreenDisplayLevel":
                {
                    OverlayLevel = Convert.ToInt16(value);

                    // set OSD toggle hotkey state
                    SettingsManager.SetProperty("OnScreenDisplayToggle", Convert.ToBoolean(value));

                    if (OverlayLevel > 0)
                    {
                        // set lastOSDLevel to be used in OSD toggle hotkey
                        SettingsManager.SetProperty("LastOnScreenDisplayLevel", value);

                        if (OverlayLevel == 5)
                        {
                            // No need to update OSD in External
                            RefreshTimer.Stop();

                            // Remove previous UI in External
                            foreach (var pair in OnScreenDisplay)
                            {
                                var processOSD = pair.Value;
                                processOSD.Update("");
                            }
                        }
                        else
                        {
                            // Other modes need the refresh timer to update OSD
                            if (!RefreshTimer.IsRunning())
                                RefreshTimer.Start();
                        }
                    }
                    else
                    {
                        RefreshTimer.Stop();

                        // clear UI on stop
                        foreach (var pair in OnScreenDisplay)
                        {
                            var processOSD = pair.Value;
                            processOSD.Update("");
                        }
                    }
                }
                break;

            case "OnScreenDisplayOrder":
                OverlayOrder = value.ToString().Split(",");
                OverlayCount = OverlayOrder.Length;
                break;
            case "OnScreenDisplayTimeLevel":
                OverlayTimeLevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayFPSLevel":
                OverlayFPSLevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayCPULevel":
                OverlayCPULevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayRAMLevel":
                OverlayRAMLevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayGPULevel":
                OverlayGPULevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayVRAMLevel":
                OverlayVRAMLevel = Convert.ToInt16(value);
                break;
            case "OnScreenDisplayBATTLevel":
                OverlayBATTLevel = Convert.ToInt16(value);
                break;
        }
    }
}

public struct OverlayEntryElement
{
    public string Value { get; set; }
    public string SzUnit { get; set; }

    public override string ToString()
    {
        return string.Format("<C0>{0:00}<S1>{1}<S><C>", Value, SzUnit);
    }

    public OverlayEntryElement(float value, String unit)
    {
        Value = string.Format("{0:00}", value);
        SzUnit = unit;
    }

    public OverlayEntryElement(string value, String unit)
    {
        Value = value;
        SzUnit = unit;
    }
}

public class OverlayEntry : IDisposable
{
    public List<OverlayEntryElement> elements = [];

    public OverlayEntry(string name, string colorScheme = "", bool indent = false)
    {
        Name = indent ? name + "\t" : name;

        if (!string.IsNullOrEmpty(colorScheme))
            Name = "<C=" + colorScheme + ">" + Name + "<C>";
    }

    public string Name { get; set; }

    public void Dispose()
    {
        elements.Clear();
        elements = null;
    }
}

public class OverlayRow : IDisposable
{
    public List<OverlayEntry> entries = [];

    public void Dispose()
    {
        entries.Clear();
        entries = null;
    }

    public override string ToString()
    {
        List<string> rowStr = [];

        foreach (var entry in entries)
        {
            if (entry.elements is null || entry.elements.Count == 0)
                continue;

            List<string> entriesStr = [entry.Name];

            foreach (var element in entry.elements)
                entriesStr.Add(element.ToString());

            var ItemStr = string.Join(" ", entriesStr);
            rowStr.Add(ItemStr);
        }

        return string.Join("<C1> | <C>", rowStr);
    }
}