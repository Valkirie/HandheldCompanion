using ControllerCommon.Managers;
using ControllerCommon.Processor;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public static class SettingsManager
    {
        public static bool IsInitialized { get; internal set; }
        private static Dictionary<string, object> Settings = new();

        public static event SettingValueChangedEventHandler SettingValueChanged;
        public delegate void SettingValueChangedEventHandler(string name, object value);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static SettingsManager()
        {
        }

        public static void Start()
        {
            var properties = Properties.Settings
                .Default
                .Properties
                .Cast<System.Configuration.SettingsProperty>()
                .OrderBy(s => s.Name);

            foreach (SettingsProperty property in properties)
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
            object prevValue = GetProperty(name, temporary);
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
            catch { }
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
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");

                        double TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideDown"]);
                        return TDPoverride ? Properties.Settings.Default["ConfigurableTDPOverrideDown"] : MainWindow.CurrentDevice.cTDP[0];
                    }

                case "ConfigurableTDPOverrideUp":
                    {
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");

                        double TDPvalue = Convert.ToDouble(Properties.Settings.Default["ConfigurableTDPOverrideUp"]);
                        return TDPoverride ? Properties.Settings.Default["ConfigurableTDPOverrideUp"] : MainWindow.CurrentDevice.cTDP[1];
                    }

                case "QuickToolsPerformanceTDPSustainedValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                        double TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPSustainedValue"]);
                        return TDPvalue != 0 ? Properties.Settings.Default["QuickToolsPerformanceTDPSustainedValue"] : MainWindow.CurrentDevice.nTDP[(int)PowerType.Slow];
                    }

                case "QuickToolsPerformanceTDPBoostValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");

                        double TDPvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"]);
                        return TDPvalue != 0 ? Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"] : MainWindow.CurrentDevice.nTDP[(int)PowerType.Fast];
                    }

                case "QuickToolsPerformanceGPUValue":
                    {
                        bool GPUoverride = GetBoolean("QuickToolsPerformanceGPUEnabled");

                        double GPUvalue = Convert.ToDouble(Properties.Settings.Default["QuickToolsPerformanceGPUValue"]);
                        return GPUvalue;
                    }

                default:
                    {
                        if (temporary && Settings.ContainsKey(name))
                            return Settings[name];
                        else if (PropertyExists(name))
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
}
