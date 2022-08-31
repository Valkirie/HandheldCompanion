using ControllerCommon.Managers;
using ControllerCommon.Processor;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace HandheldCompanion.Managers
{
    public static class SettingsManager
    {
        public static bool IsInitialized { get; internal set; }

        public static event SettingValueChangedEventHandler SettingValueChanged;
        public delegate void SettingValueChangedEventHandler(string name, object value);

        public static void Start()
        {
            foreach (SettingsProperty property in Properties.Settings.Default.Properties)
                SettingValueChanged(property.Name, GetProperty(property.Name));

            IsInitialized = true;
        }

        public static void SetProperty(string name, object value)
        {
            string prevValue = Convert.ToString(Properties.Settings.Default[name]);
            string strValue = Convert.ToString(value);

            if (prevValue == strValue)
                return;

            switch (name)
            {
                case "OverlayControllerBackgroundColor":
                    value = Convert.ToString(value);
                    break;
            }

            Properties.Settings.Default[name] = value;
            Properties.Settings.Default.Save();

            SettingValueChanged?.Invoke(name, value);

            LogManager.LogDebug("Settings {0} set to {1}", name, value);
        }

        public static Dictionary<string, object> GetProperties()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            foreach (SettingsProperty property in Properties.Settings.Default.Properties)
                result.Add(property.Name, GetProperty(property.Name));

            return result;
        }

        private static object GetProperty(string name)
        {
            // used to handle cases
            switch (name)
            {
                case "ConfigurableTDPOverrideDown":
                    {
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                        return TDPoverride ? Properties.Settings.Default["ConfigurableTDPOverrideDown"] : MainWindow.handheldDevice.cTDP[0];
                    }

                case "ConfigurableTDPOverrideUp":
                    {
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                        return TDPoverride ? Properties.Settings.Default["ConfigurableTDPOverrideUp"] : MainWindow.handheldDevice.cTDP[1];
                    }

                case "QuickToolsPerformanceTDPSustainedValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");
                        return TDPoverride ? Properties.Settings.Default["QuickToolsPerformanceTDPSustainedValue"] : MainWindow.handheldDevice.nTDP[(int)PowerType.Slow];
                    }

                case "QuickToolsPerformanceTDPBoostValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");
                        return TDPoverride ? Properties.Settings.Default["QuickToolsPerformanceTDPBoostValue"] : MainWindow.handheldDevice.nTDP[(int)PowerType.Fast];
                    }

                case "QuickToolsPerformanceGPUValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceGPUEnabled");
                        return TDPoverride ? Properties.Settings.Default["QuickToolsPerformanceGPUValue"] : 0;
                    }

                default:
                    return Properties.Settings.Default[name];
            }

            return null;
        }

        public static string GetString(string name)
        {
            return Convert.ToString(GetProperty(name));
        }

        public static bool GetBoolean(string name)
        {
            return Convert.ToBoolean(GetProperty(name));
        }

        public static int GetInt(string name)
        {
            return Convert.ToInt32(GetProperty(name));
        }

        public static DateTime GetDateTime(string name)
        {
            return Convert.ToDateTime(GetProperty(name));
        }

        public static double GetDouble(string name)
        {
            return Convert.ToDouble(GetProperty(name));
        }
    }
}
