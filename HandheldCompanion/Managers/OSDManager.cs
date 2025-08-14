using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Shared;

using RTSSSharedMemoryNET;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Timers;

namespace HandheldCompanion.Managers;

public static class OSDManager
{
    public delegate void InitializedEventHandler();
    public static event InitializedEventHandler Initialized;

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

    private static readonly Timer RefreshTimer;
    private static int RefreshInterval = 100;

    private static readonly ConcurrentDictionary<int, OSD> OnScreenDisplay = new();
    private static AppEntry OnScreenAppEntry;
    private static List<string> Content = new();

    static OSDManager()
    {
        RefreshTimer = new Timer(RefreshInterval) { AutoReset = true };
        RefreshTimer.Elapsed += UpdateOSD;
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        if (OverlayLevel != 0 && !RefreshTimer.Enabled)
            RefreshTimer.Start();

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        switch (ManagerFactory.platformManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPlatforms();
                break;
        }

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "OSDManager");
    }

    private static void QueryPlatforms()
    {
        // manage events
        PlatformManager.RTSS.Hooked += RTSS_Hooked;
        PlatformManager.RTSS.Unhooked += RTSS_Unhooked;

        AppEntry appEntry = PlatformManager.RTSS.GetAppEntry();
        if (appEntry is not null)
            RTSS_Hooked(appEntry);
    }

    private static void PlatformManager_Initialized()
    {
        QueryPlatforms();
    }

    private static void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private static void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("OnScreenDisplayRefreshRate", ManagerFactory.settingsManager.GetString("OnScreenDisplayRefreshRate"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayLevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayOrder", ManagerFactory.settingsManager.GetString("OnScreenDisplayOrder"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayTimeLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayTimeLevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayFPSLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayFPSLevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayCPULevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayCPULevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayRAMLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayRAMLevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayGPULevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayGPULevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayVRAMLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayVRAMLevel"), false);
        SettingsManager_SettingValueChanged("OnScreenDisplayBATTLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayBATTLevel"), false);
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        RefreshTimer.Stop();

        // unhook all processes
        foreach (var processId in OnScreenDisplay.Keys)
            RTSS_Unhooked(processId);

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        PlatformManager.RTSS.Hooked -= RTSS_Hooked;
        PlatformManager.RTSS.Unhooked -= RTSS_Unhooked;

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "OSDManager");
    }

    private static void RTSS_Unhooked(int processId)
    {
        try
        {
            // clear previous display
            if (OnScreenDisplay.TryGetValue(processId, out OSD? OSD))
            {
                if (OSD is not null)
                {
                    OSD.Update(string.Empty);
                    OSD.Dispose();
                }

                OnScreenDisplay.Remove(processId, out _);
            }
        }
        catch { }
    }

    private static void RTSS_Hooked(AppEntry appEntry)
    {
        if (appEntry is null)
            return;

        try
        {
            // update foreground id
            OnScreenAppEntry = appEntry;

            // only create a new OSD if needed
            if (OnScreenDisplay.ContainsKey(appEntry.ProcessId))
                return;

            OnScreenDisplay[OnScreenAppEntry.ProcessId] = new OSD(OnScreenAppEntry.Name);
        }
        catch { }
    }

    private static void UpdateOSD(object? sender, ElapsedEventArgs e)
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
            catch { }
        }
    }

    public static string Draw(int processId)
    {
        // prepare content
        Content.Clear();

        GPU gpu = GPUManager.GetCurrent();
        if (gpu is null)
            goto Exit;

        // we're not ready yet
        if (!ManagerFactory.platformManager.IsReady)
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
                    AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardware?.GetGPULoad(), "%");
                    AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardware?.GetGPUPower(), "W");
                    row1.entries.Add(GPUentry);

                    OverlayEntry VRAMentry = new("VRAM", "8000FF");
                    AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardware?.GetGPUMemory(), "GB");
                    row1.entries.Add(VRAMentry);

                    OverlayEntry CPUentry = new("CPU", "80FF");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardware?.GetCPULoad(), "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardware?.GetCPUPower(), "W");
                    row1.entries.Add(CPUentry);
                    OverlayEntry RAMentry = new("RAM", "FF80C0");
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardware?.GetMemoryUsage(), "GB");
                    row1.entries.Add(RAMentry);

                    OverlayEntry BATTentry = new("BATT", "FF8000");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardware?.GetBatteryLevel(), "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardware?.GetBatteryTimeSpan(), "min");
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
                    AddElementIfNotNull(GPUentry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardware?.GetGPULoad(), "%");
                    AddElementIfNotNull(GPUentry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardware?.GetGPUPower(), "W");
                    AddElementIfNotNull(GPUentry, gpu.HasTemperature() ? gpu.GetTemperature() : PlatformManager.LibreHardware?.GetGPUTemperature(), "C");
                    row1.entries.Add(GPUentry);

                    OverlayEntry VRAMentry = new("VRAM", "8000FF", true);
                    AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardware?.GetGPUMemory(), PlatformManager.LibreHardware?.GetGPUMemoryTotal(), "GB");
                    row4.entries.Add(VRAMentry);

                    OverlayEntry CPUentry = new("CPU", "80FF", true);
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardware?.GetCPULoad(), "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardware?.GetCPUPower(), "W");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardware?.GetCPUTemperature(), "C");
                    row2.entries.Add(CPUentry);

                    OverlayEntry RAMentry = new("RAM", "FF80C0", true);
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardware?.GetMemoryUsage(), PlatformManager.LibreHardware?.GetMemoryTotal(), "GB");
                    row3.entries.Add(RAMentry);

                    OverlayEntry BATTentry = new("BATT", "FF8000", true);
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardware?.GetBatteryLevel(), "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardware?.GetBatteryPower(), "W");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardware?.GetBatteryTimeSpan(), "min");
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
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetCPULoad(), "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetCPUPower(), "W");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetCPUTemperature(), "C");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetCPULoad(), "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetCPUPower(), "W");
                        break;
                }
                break;
            case "RAM":
                switch (OverlayRAMLevel)
                {
                    case 2:
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetMemoryUsage(), "GB");
                        break;
                }
                break;
            case "GPU":
                switch (OverlayGPULevel)
                {
                    case 2:
                        AddElementIfNotNull(entry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardware?.GetGPULoad(), "%");
                        AddElementIfNotNull(entry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardware?.GetGPUPower(), "W");
                        AddElementIfNotNull(entry, gpu.HasTemperature() ? gpu.GetTemperature() : PlatformManager.LibreHardware?.GetGPUTemperature(), "C");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, gpu.HasLoad() ? gpu.GetLoad() : PlatformManager.LibreHardware?.GetGPULoad(), "%");
                        AddElementIfNotNull(entry, gpu.HasPower() ? gpu.GetPower() : PlatformManager.LibreHardware?.GetGPUPower(), "W");
                        break;
                }
                break;
            case "VRAM":
                switch (OverlayVRAMLevel)
                {
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetGPUMemory(), "GB");
                        break;
                    case 2:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetGPUMemory(), PlatformManager.LibreHardware?.GetGPUMemoryTotal(), "GB");
                        break;
                }
                break;
            case "BATT":
                switch (OverlayBATTLevel)
                {
                    case 2:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetBatteryLevel(), "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetBatteryPower(), "W");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetBatteryTimeSpan(), "min");
                        break;
                    case 1:
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetBatteryLevel(), "%");
                        AddElementIfNotNull(entry, PlatformManager.LibreHardware?.GetBatteryTimeSpan(), "min");
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

    private static void AddElementIfNotNull(OverlayEntry entry, float? value, string unit)
    {
        if (value is not null)
            entry.elements.Add(new OverlayEntryElement((float)value, unit));
    }

    private static void AddElementIfNotNull(OverlayEntry entry, float? value, float? available, string unit)
    {
        if (value is not null && available is not null)
            entry.elements.Add(new OverlayEntryElement((float)value, (float)available, unit));
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "OnScreenDisplayRefreshRate":
                {
                    RefreshInterval = Convert.ToInt32(value);

                    if (RefreshTimer.Enabled)
                    {
                        RefreshTimer.Stop();
                        RefreshTimer.Interval = RefreshInterval;
                        RefreshTimer.Start();
                    }
                }
                break;

            case "OnScreenDisplayLevel":
                {
                    OverlayLevel = Convert.ToInt16(value);

                    // set OSD toggle hotkey state
                    ManagerFactory.settingsManager.SetProperty("OnScreenDisplayToggle", OverlayLevel != 0);

                    if (OverlayLevel > 0)
                    {
                        // set lastOSDLevel to be used in OSD toggle hotkey
                        ManagerFactory.settingsManager.SetProperty("LastOnScreenDisplayLevel", value);

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
                            if (!RefreshTimer.Enabled)
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

    public OverlayEntryElement(float value, string unit)
    {
        Value = FormatValue(value, unit);
        SzUnit = unit;
    }

    public OverlayEntryElement(float value, float available, string unit)
    {
        Value = FormatValue(value, unit) + "/" + FormatValue(available, unit);
        SzUnit = unit;
    }

    private static string FormatValue(float value, string unit)
    {
        string format = unit switch
        {
            "GB" => "0.0", // One decimal
            "W" => "00",   // Two digits forced, no decimal
            "%" => "00",   // Two digits forced, no decimal
            "C" => "00",   // Two digits forced, no decimal
            "MB" => "0",   // No leading zeros, no decimal
            _ => "0.##"    // Default format (no leading zeros, up to 2 decimals)
        };

        return value.ToString(format);
    }

    public OverlayEntryElement(string value, string unit)
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

    ~OverlayEntry()
    {
        Dispose();
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

    ~OverlayRow()
    {
        Dispose();
    }

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