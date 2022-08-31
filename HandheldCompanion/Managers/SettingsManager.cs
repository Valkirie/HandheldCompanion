using ControllerCommon.Managers;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        public static void SetProperty(string name, object value)
        {
            string prevValue = Convert.ToString(Properties.Settings.Default[name]);
            string strValue = Convert.ToString(value);

            if (prevValue == strValue)
                return;

            switch(name)
            {
                case "OverlayControllerBackgroundColor":
                    value = Convert.ToString(value);
                    break;
            }

            Properties.Settings.Default[name] = value;
            Properties.Settings.Default.Save();

            SettingValueChanged(name, value);

            LogManager.LogDebug("Settings {0} set to {1}", name, value);
        }

        private static object GetProperty(string name)
        {
            // used to handle cases
            switch(name)
            {
                case "ConfigurableTDPOverrideDown":
                    {
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                        return TDPoverride ? GetProperty("ConfigurableTDPOverrideDown") : MainWindow.handheldDevice.cTDP[0];
                    }

                case "ConfigurableTDPOverrideUp":
                    {
                        bool TDPoverride = GetBoolean("ConfigurableTDPOverride");
                        return TDPoverride ? GetProperty("ConfigurableTDPOverrideUp") : MainWindow.handheldDevice.cTDP[1];
                    }

                case "QuickToolsPerformanceTDPSustainedValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");
                        return TDPoverride ? GetProperty("QuickToolsPerformanceTDPSustainedValue") : 0;
                    }

                case "QuickToolsPerformanceTDPBoostValue":
                    {
                        bool TDPoverride = GetBoolean("QuickToolsPerformanceTDPEnabled");
                        return TDPoverride ? GetProperty("QuickToolsPerformanceTDPBoostValue") : 0;
                    }

                case "QuickToolsPerformanceGPUValue":
                    {
                        bool TDPoverride = Convert.ToBoolean(GetProperty("QuickToolsPerformanceGPUEnabled"));
                        return TDPoverride ? GetProperty("QuickToolsPerformanceGPUValue") : 0;
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
