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
    public class SettingsManager : Manager
    {
        public event SettingValueChangedEventHandler SettingValueChanged;
        public delegate void SettingValueChangedEventHandler(string name, object value);

        public override void Start()
        {
            foreach (SettingsProperty property in Properties.Settings.Default.Properties)
                SettingValueChanged(property.Name, GetProperty(property.Name));

            base.Start();
        }

        public void SetProperty(string name, object value)
        {
            string prevValue = Convert.ToString(Properties.Settings.Default[name]);
            string strValue = Convert.ToString(value);

            if (prevValue == strValue)
                return;

            Properties.Settings.Default[name] = value;
            Properties.Settings.Default.Save();

            SettingValueChanged(name, value);

            LogManager.LogDebug("Settings {0} set to {1}", name, value);
        }

        public object GetProperty(string name)
        {
            // used to handle cases
            switch(name)
            {
                case "ConfigurableTDPOverrideDown":
                    {
                        bool TDPoverride = Convert.ToBoolean(GetProperty("ConfigurableTDPOverride"));
                        return TDPoverride ? GetProperty("ConfigurableTDPOverrideDown") : MainWindow.handheldDevice.cTDP[0];
                    }

                case "ConfigurableTDPOverrideUp":
                    {
                        bool TDPoverride = Convert.ToBoolean(GetProperty("ConfigurableTDPOverride"));
                        return TDPoverride ? GetProperty("ConfigurableTDPOverrideUp") : MainWindow.handheldDevice.cTDP[1];
                    }

                default:
                    return Properties.Settings.Default[name];
            }

            return null;
        }
    }
}
