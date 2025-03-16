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
    public static readonly string OnScreenDisplayLevel = "OnScreenDisplayLevel";
    public static readonly string OnScreenDisplayTimeLevel = "OnScreenDisplayTimeLevel";
    public static readonly string OnScreenDisplayFPSLevel = "OnScreenDisplayFPSLevel";
    public static readonly string OnScreenDisplayCPULevel = "OnScreenDisplayCPULevel";
    public static readonly string OnScreenDisplayGPULevel = "OnScreenDisplayGPULevel";
    public static readonly string OnScreenDisplayRAMLevel = "OnScreenDisplayRAMLevel";
    public static readonly string OnScreenDisplayVRAMLevel = "OnScreenDisplayVRAMLevel";
    public static readonly string OnScreenDisplayBATTLevel = "OnScreenDisplayBATTLevel";

    /// <summary>
    /// First version that implemented the new hotkey manager
    /// </summary>
    public static readonly string VersionHotkeyManager = "0.21.5.0";
}

public enum LayoutModes
{
    Gamepad = 0,
    Desktop = 1,
    Auto = 2
}

public class SettingsManager : IManager
{
    public delegate void SettingValueChangedEventHandler(string name, object value, bool temporary);
    public event SettingValueChangedEventHandler SettingValueChanged;

    private readonly Dictionary<string, object> Settings = [];

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
            SettingValueChanged(property.Name, GetProperty(property.Name), false);

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

    public void SetProperty(string name, object value, bool force = false, bool temporary = false)
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
                    if (prevValue.ToString() == value.ToString() && !force)
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

            LogManager.LogDebug("Settings {0} set to {1}", name, value);
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
                    var TDPoverride = GetBoolean("ConfigurableTDPOverride");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideDown"]);
                    return TDPoverride
                        ? Properties.Settings.Default["ConfigurableTDPOverrideDown"]
                        : IDevice.GetCurrent().cTDP[0];
                }

            case "ConfigurableTDPOverrideUp":
                {
                    var TDPoverride = GetBoolean("ConfigurableTDPOverride");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideUp"]);
                    return TDPoverride
                        ? Properties.Settings.Default["ConfigurableTDPOverrideUp"]
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
                        return property;
                    if (PropertyExists(name))
                        return Properties.Settings.Default[name];

                    return false;
                }
        }
    }

    public string GetString(string name, bool temporary = false)
    {
        return Convert.ToString(GetProperty(name, temporary));
    }

    public bool GetBoolean(string name, bool temporary = false)
    {
        return Convert.ToBoolean(GetProperty(name, temporary));
    }

    public Color GetColor(string name, bool temporary = false)
    {
        // Conver color, which is stored as a HEX string to a color datatype
        string hexColor = Convert.ToString(GetProperty(name, temporary));

        // Remove the '#' character and convert the remaining string to a 32-bit integer
        var argbValue = int.Parse(hexColor.Substring(1), System.Globalization.NumberStyles.HexNumber);

        // Extract alpha, red, green, and blue components
        byte alpha = (byte)((argbValue >> 24) & 0xFF);
        byte red = (byte)((argbValue >> 16) & 0xFF);
        byte green = (byte)((argbValue >> 8) & 0xFF);
        byte blue = (byte)(argbValue & 0xFF);

        // Create a Color object from the extracted components
        Color color = Color.FromArgb(alpha, red, green, blue);

        return color;
    }

    public int GetInt(string name, bool temporary = false)
    {
        return Convert.ToInt32(GetProperty(name, temporary));
    }

    public uint GetUInt(string name, bool temporary = false)
    {
        return Convert.ToUInt32(GetProperty(name, temporary));
    }

    public DateTime GetDateTime(string name, bool temporary = false)
    {
        return Convert.ToDateTime(GetProperty(name, temporary));
    }

    public double GetDouble(string name, bool temporary = false)
    {
        return Convert.ToDouble(GetProperty(name, temporary));
    }

    public StringCollection GetStringCollection(string name, bool temporary = false)
    {
        return (StringCollection)GetProperty(name, temporary);
    }
}