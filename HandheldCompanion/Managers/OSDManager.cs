using PrecisionTiming;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
        "<C0=FFFFFF><C1=458A6E><C2=4C8DB2><C3=AD7B95><C4=A369A6><C5=F19F86><C6=D76D76><A0=-4><A1=5><A2=-2><A3=-3><A4=-4><A5=-5><S0=-50><S1=50>";

    private static bool IsInitialized;
    public static short OverlayLevel;

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
        Content = new List<string>();
        switch (OverlayLevel)
        {
            default:
            case 0: // Disabled
                break;

            case 1: // Minimal
                {
                    OverlayRow row1 = new();

                    OverlayEntry FPSentry = new("<APP>", "C6");
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FR>",
                        SzUnit = "FPS"
                    });
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FT>",
                        SzUnit = "ms"
                    });
                    row1.entries.Add(FPSentry);

                    // add header to row1
                    Content.Add(Header + row1);
                }
                break;

            case 2: // Extended
                {
                    OverlayRow row1 = new();

                    OverlayEntry GPUentry = new("GPU", "C1");
                    AddElementIfNotNull(GPUentry, PlatformManager.LibreHardwareMonitor.GPULoad, "%");
                    AddElementIfNotNull(GPUentry, PlatformManager.LibreHardwareMonitor.GPUPower, "W");
                    row1.entries.Add(GPUentry);

                    OverlayEntry CPUentry = new("CPU", "C2");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                    row1.entries.Add(CPUentry);

                    OverlayEntry RAMentry = new("RAM", "C3");
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.MemoryLoad, "%");
                    row1.entries.Add(RAMentry);

                    OverlayEntry BATTentry = new("BATT", "C5");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                    row1.entries.Add(BATTentry);

                    OverlayEntry FPSentry = new("<APP>", "C6");
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FR>",
                        SzUnit = "FPS"
                    });
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FT>",
                        SzUnit = "ms"
                    });
                    row1.entries.Add(FPSentry);

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

                    OverlayEntry GPUentry = new("GPU", "C1", true);
                    AddElementIfNotNull(GPUentry, PlatformManager.LibreHardwareMonitor.GPULoad, "%");
                    AddElementIfNotNull(GPUentry, PlatformManager.LibreHardwareMonitor.GPUPower, "W");
                    AddElementIfNotNull(GPUentry, PlatformManager.LibreHardwareMonitor.GPUTemperatur, "C");
                    row1.entries.Add(GPUentry);

                    OverlayEntry CPUentry = new("CPU", "C2", true);
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPULoad, "%");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUPower, "W");
                    AddElementIfNotNull(CPUentry, PlatformManager.LibreHardwareMonitor.CPUTemperatur, "C");
                    row2.entries.Add(CPUentry);

                    OverlayEntry RAMentry = new("RAM", "C3", true);
                    AddElementIfNotNull(RAMentry, PlatformManager.LibreHardwareMonitor.MemoryLoad, "%");
                    row3.entries.Add(RAMentry);

                    OverlayEntry VRAMentry = new("VRAM", "C4", true);
                    AddElementIfNotNull(VRAMentry, PlatformManager.LibreHardwareMonitor.VRAMLoad, "%");
                    row4.entries.Add(VRAMentry);

                    OverlayEntry BATTentry = new("BATT", "C5", true);
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryLevel, "%");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryPower, "W");
                    AddElementIfNotNull(BATTentry, PlatformManager.LibreHardwareMonitor.BatteryTimeSpan, "min");
                    row5.entries.Add(BATTentry);

                    OverlayEntry FPSentry = new("<APP>", "C6");
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FR>",
                        SzUnit = "FPS"
                    });
                    FPSentry.elements.Add(new OverlayEntryElement
                    {
                        Value = "<FT>",
                        SzUnit = "ms"
                    });
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

            case 4: // External
                {
                    /*
                     * Intended to simply allow RTSS/HWINFO to run, and let the user configure the overlay within those
                     * tools as they wish
                     */
                }
                break;
        }

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

    private static void AddElementIfNotNull(OverlayEntry entry, float? value, String unit)
    {
        if (value is not null)
        {
            entry.elements.Add(new OverlayEntryElement((float)value, unit));
        }
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        switch (name)
        {
            case "OnScreenDisplayLevel":
                {
                    OverlayLevel = Convert.ToInt16(value);

                    // set OSD toggle hotkey state
                    SettingsManager.SetProperty("OnScreenDisplayToggle", Convert.ToBoolean(value));

                    if (OverlayLevel > 0)
                    {
                        // set lastOSDLevel to be used in OSD toggle hotkey
                        SettingsManager.SetProperty("LastOnScreenDisplayLevel", value);
                        
                        if (OverlayLevel == 4)
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
}

public class OverlayEntry : IDisposable
{
    public List<OverlayEntryElement> elements = new();

    public OverlayEntry(string name, string colorScheme = "", bool indent = false)
    {
        Name = indent ? name + "\t" : name;

        if (!string.IsNullOrEmpty(colorScheme))
            Name = "<" + colorScheme + ">" + Name + "<C>";
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
    public List<OverlayEntry> entries = new();

    public void Dispose()
    {
        entries.Clear();
        entries = null;
    }

    public override string ToString()
    {
        List<string> rowStr = new();

        foreach (var entry in entries)
        {
            if (entry.elements is null || entry.elements.Count == 0)
                continue;

            List<string> entriesStr = new() { entry.Name };

            foreach (var element in entry.elements)
                entriesStr.Add(element.ToString());

            var ItemStr = string.Join(" ", entriesStr);
            rowStr.Add(ItemStr);
        }

        return string.Join(" | ", rowStr);
    }
}