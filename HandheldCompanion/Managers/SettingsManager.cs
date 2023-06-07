﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using HandheldCompanion.Views;

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
        if (prevValue.ToString() == value.ToString() && !force)
            return;

        // specific cases
        switch (name)
        {
            case "OverlayControllerBackgroundColor":
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

    public static int GetInt(string name, bool temporary = false)
    {
        return Convert.ToInt32(GetProperty(name, temporary));
    }

    public static DateTime GetDateTime(string name, bool temporary = false)
    {
        return Convert.ToDateTime(GetProperty(name, temporary));
    }

    public static double GetDouble(string name, bool temporary = false)
    {
        return Convert.ToDouble(GetProperty(name, temporary));
    }
}