using HandheldCompanion.Devices;
using HandheldCompanion.Processors;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace HandheldCompanion.Managers;

public static class Settings
{
    public static readonly string ConfigurableTDPOverrideDown = "ConfigurableTDPOverrideDown";
    public static readonly string ConfigurableTDPOverrideUp = "ConfigurableTDPOverrideUp";

    public static readonly string LibraryPageEnabled = "LibraryPageEnabled";
    public static readonly string PerformanceManagerEnabled = "PerformanceManagerEnabled";
    public static readonly string GPUManagementEnabled = "GPUManagementEnabled";

    public static readonly string OnScreenDisplayRefreshRate = "OnScreenDisplayRefreshRate";
    public static readonly string OnScreenDisplayLevel = "OnScreenDisplayLevel";
    public static readonly string OnScreenDisplayTimeLevel = "OnScreenDisplayTimeLevel";
    public static readonly string OnScreenDisplayFPSLevel = "OnScreenDisplayFPSLevel";
    public static readonly string OnScreenDisplayCPULevel = "OnScreenDisplayCPULevel";
    public static readonly string OnScreenDisplayGPULevel = "OnScreenDisplayGPULevel";
    public static readonly string OnScreenDisplayRAMLevel = "OnScreenDisplayRAMLevel";
    public static readonly string OnScreenDisplayVRAMLevel = "OnScreenDisplayVRAMLevel";
    public static readonly string OnScreenDisplayBATTLevel = "OnScreenDisplayBATTLevel";

    /// <summary>
    /// First version that implemented the new Hotkey manager
    /// </summary>
    public static readonly string VersionHotkeyManager = "0.21.5.0";

    /// <summary>
    /// First version that implemented Library manager
    /// </summary>
    public static readonly string VersionLibraryManager = "0.24.0.0";
}

public enum LayoutModes
{
    Gamepad = 0,
    Desktop = 1,
    Auto = 2
}

public class SettingsManager : IManager
{
    public delegate void SettingValueChangedEventHandler(string name, object? value, bool temporary);
    public event SettingValueChangedEventHandler? SettingValueChanged;

    private readonly Dictionary<string, object?> Settings = [];

    public SettingsManager()
    {
        if (!Directory.Exists(App.SettingsPath))
            Directory.CreateDirectory(App.SettingsPath);
    }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        IOrderedEnumerable<SettingsProperty> properties = Properties.Settings.Default.Properties.Cast<SettingsProperty>().OrderBy(s => s.Name);
        foreach (var property in properties)
            SettingValueChanged?.Invoke(property.Name, GetProperty(property.Name), false);

        if (GetBoolean("FirstStart"))
            SetProperty("FirstStart", false);

        base.Start();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();
        base.Stop();
    }

    public void SetProperty(string name, object? value, bool force = false, bool temporary = false)
    {
        var prevValue = GetProperty(name, temporary);

        if (prevValue is not null)
        {
            switch (prevValue.GetType().Name)
            {
                case "StringCollection":
                    if (prevValue.Equals(value) && !force)
                        return;
                    break;
                default:
                    if (prevValue.ToString() == value?.ToString() && !force)
                        return;
                    break;
            }
        }

        // specific cases
        switch (name)
        {
            case "OverlayControllerBackgroundColor":
            case "LEDMainColor":
            case "LEDSecondColor":
                value = Convert.ToString(value);
                break;
        }

        try
        {
            if (!temporary)
            {
                Properties.Settings.Default[name] = value;
                Properties.Settings.Default.Save();
            }

            // update internal settings dictionary (used for temporary settings)
            Settings[name] = value;

            // raise event
            if (Status.HasFlag(ManagerStatus.Initialized) || force)
                SettingValueChanged?.Invoke(name, value, temporary);

            LogManager.LogDebug("Settings {0} set to {1}", name, value ?? "null");
        }
        catch (Exception) { }
    }

    private bool PropertyExists(string name)
    {
        return Properties.Settings.Default.Properties.Cast<SettingsProperty>().Any(prop => prop.Name == name);
    }

    public SortedDictionary<string, object> GetProperties()
    {
        SortedDictionary<string, object> result = [];

        foreach (SettingsProperty property in Properties.Settings.Default.Properties)
            result.Add(property.Name, GetProperty(property.Name));

        return result;
    }

    private object GetProperty(string name, bool temporary = false)
    {
        // used to handle cases
        switch (name)
        {
            case "ConfigurableTDPOverrideDown":
                {
                    bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                    double TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideDown"]);
                    return TDPoverride && TDPvalue != 0.0d
                        ? TDPvalue
                        : IDevice.GetCurrent().cTDP[0];
                }

            case "ConfigurableTDPOverrideUp":
                {
                    bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                    double TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideUp"]);
                    return TDPoverride && TDPvalue != 0.0d
                        ? TDPvalue
                        : IDevice.GetCurrent().cTDP[1];
                }

            case "QuickToolsPerformanceTDPValue":
                {
                    var TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPValue"]);
                    return TDPvalue != 0
                        ? Properties.Settings.Default["QuickToolsPerformanceTDPValue"]
                        : IDevice.GetCurrent().nTDP[(int)PowerType.Slow];
                }

            case "QuickToolsPerformanceTDPBoostValue":
                {
                    var TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"]);
                    return TDPvalue != 0
                        ? Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"]
                        : IDevice.GetCurrent().nTDP[(int)PowerType.Fast];
                }

            case "QuickToolsPerformanceGPUValue":
                {
                    var GPUoverride = GetBoolean("QuickToolsPerformanceGPUEnabled");

                    var GPUvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceGPUValue"]);
                    return GPUvalue;
                }

            case "HasBrightnessSupport":
                return ManagerFactory.multimediaManager.HasBrightnessSupport();

            case "HasVolumeSupport":
                return ManagerFactory.multimediaManager.HasVolumeSupport();

            default:
                {
                    if (temporary && Settings.TryGetValue(name, out var property))
                        return property ?? false;
                    if (PropertyExists(name))
                        return Properties.Settings.Default[name] ?? false;

                    return false;
                }
        }
    }

    public string GetString(string name, bool temporary = false)
    {
        try { return Convert.ToString(GetProperty(name, temporary)) ?? string.Empty; }
        catch { return string.Empty; }
    }

    public bool GetBoolean(string name, bool temporary = false)
    {
        try { return Convert.ToBoolean(GetProperty(name, temporary)); }
        catch { return false; }
    }

    public Color GetColor(string name, bool temporary = false)
    {
        try
        {
            string? hexColor = Convert.ToString(GetProperty(name, temporary));
            if (hexColor is null)
                return Colors.Transparent;

            int argbValue = int.Parse(hexColor.Substring(1), System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb(
                (byte)((argbValue >> 24) & 0xFF),
                (byte)((argbValue >> 16) & 0xFF),
                (byte)((argbValue >> 8) & 0xFF),
                (byte)(argbValue & 0xFF));
        }
        catch { return Colors.Transparent; }
    }

    public int GetInt(string name, bool temporary = false)
    {
        try { return Convert.ToInt32(GetProperty(name, temporary)); }
        catch { return 0; }
    }

    public uint GetUInt(string name, bool temporary = false)
    {
        try { return Convert.ToUInt32(GetProperty(name, temporary)); }
        catch { return 0u; }
    }

    public DateTime GetDateTime(string name, bool temporary = false)
    {
        try { return Convert.ToDateTime(GetProperty(name, temporary)); }
        catch { return default; }
    }

    public double GetDouble(string name, bool temporary = false)
    {
        try { return Convert.ToDouble(GetProperty(name, temporary)); }
        catch { return 0d; }
    }

    public StringCollection GetStringCollection(string name, bool temporary = false)
    {
        try { return (StringCollection)GetProperty(name, temporary); }
        catch { return []; }
    }
}