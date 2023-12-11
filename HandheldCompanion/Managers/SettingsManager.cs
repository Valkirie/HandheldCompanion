using HandheldCompanion.Processors;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
<<<<<<< HEAD

=======
using System.Windows.Media;

>>>>>>> 13793a887a48c3f3d5e7875eb624f8bfb16410cc
namespace HandheldCompanion.Managers;

public static class SettingsManager
{
    public delegate void InitializedEventHandler();

    public delegate void SettingValueChangedEventHandler(string name, object value);

    private static readonly Dictionary<string, object> Settings = new();

    static SettingsManager()
    {
    }

    public static bool IsInitialized { get; internal set; }

    public static event SettingValueChangedEventHandler SettingValueChanged;

    public static event InitializedEventHandler Initialized;

    public static void Start()
    {
        var properties = Properties.Settings
            .Default
            .Properties
            .Cast<SettingsProperty>()
            .OrderBy(s => s.Name);

        foreach (var property in properties)
            SettingValueChanged(property.Name, GetProperty(property.Name));

        if (GetBoolean("FirstStart"))
            SetProperty("FirstStart", false);

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "SettingsManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "SettingsManager");
    }

    public static void SetProperty(string name, object value, bool force = false, bool temporary = false)
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
                value = Convert.ToString(value);
                break;
            case "LEDMainColor":
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
            SettingValueChanged?.Invoke(name, value);

            LogManager.LogDebug("Settings {0} set to {1}", name, value);
        }
        catch
        {
        }
    }

    private static bool PropertyExists(string name)
    {
        return Properties.Settings.Default.Properties.Cast<SettingsProperty>().Any(prop => prop.Name == name);
    }

    public static SortedDictionary<string, object> GetProperties()
    {
        SortedDictionary<string, object> result = new();

        foreach (SettingsProperty property in Properties.Settings.Default.Properties)
            result.Add(property.Name, GetProperty(property.Name));

        return result;
    }

    private static object GetProperty(string name, bool temporary = false)
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
                        : MainWindow.CurrentDevice.cTDP[0];
                }

            case "ConfigurableTDPOverrideUp":
                {
                    var TDPoverride = GetBoolean("ConfigurableTDPOverride");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideUp"]);
                    return TDPoverride
                        ? Properties.Settings.Default["ConfigurableTDPOverrideUp"]
                        : MainWindow.CurrentDevice.cTDP[1];
                }

            case "QuickToolsPerformanceTDPValue":
                {
                    var TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPValue"]);
                    return TDPvalue != 0
                        ? Properties.Settings.Default["QuickToolsPerformanceTDPValue"]
                        : MainWindow.CurrentDevice.nTDP[(int)PowerType.Slow];
                }

            case "QuickToolsPerformanceTDPBoostValue":
                {
                    var TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                    var TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"]);
                    return TDPvalue != 0
                        ? Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"]
                        : MainWindow.CurrentDevice.nTDP[(int)PowerType.Fast];
                }

            case "QuickToolsPerformanceGPUValue":
                {
                    var GPUoverride = GetBoolean("QuickToolsPerformanceGPUEnabled");

                    var GPUvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceGPUValue"]);
                    return GPUvalue;
                }

            case "HasBrightnessSupport":
                return SystemManager.HasBrightnessSupport();

            case "HasVolumeSupport":
                return SystemManager.HasVolumeSupport();

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

    public static string GetString(string name, bool temporary = false)
    {
        return Convert.ToString(GetProperty(name, temporary));
    }

    public static bool GetBoolean(string name, bool temporary = false)
    {
        return Convert.ToBoolean(GetProperty(name, temporary));
    }

    public static Color GetColor(string name, bool temporary = false)
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

    public static int GetInt(string name, bool temporary = false)
    {
        return Convert.ToInt32(GetProperty(name, temporary));
    }

    public static uint GetUInt(string name, bool temporary = false)
    {
        return Convert.ToUInt32(GetProperty(name, temporary));
    }

    public static DateTime GetDateTime(string name, bool temporary = false)
    {
        return Convert.ToDateTime(GetProperty(name, temporary));
    }

    public static double GetDouble(string name, bool temporary = false)
    {
        return Convert.ToDouble(GetProperty(name, temporary));
    }

    public static StringCollection GetStringCollection(string name, bool temporary = false)
    {
        return (StringCollection)GetProperty(name, temporary);
    }
}