using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using System;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public class DevicePageViewModel : BaseViewModel
    {
        public bool IsUnsupportedDevice => IDevice.GetCurrent() is DefaultDevice;
        private IDevice CurrentDevice => IDevice.GetCurrent();

        public int BatteryBypassMin => CurrentDevice.BatteryBypassMin;
        public int BatteryBypassMax => CurrentDevice.BatteryBypassMax;

        public Visibility BatteryBypassVisibility => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryChargeLimit) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BatteryBypassModeVisibility => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryBypassCharging) ? Visibility.Visible : Visibility.Collapsed;

        private bool _BatteryChargeLimitPercent;
        public bool BatteryChargeLimitPercent
        {
            get
            {
                return _BatteryChargeLimitPercent;
            }
            set
            {
                if (value != _BatteryChargeLimitPercent)
                {
                    _BatteryChargeLimitPercent = value;
                    OnPropertyChanged(nameof(BatteryChargeLimitPercent));
                }
            }
        }

        public int ClawControllerIndex
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("MSIClawControllerIndex");
            }
            set
            {
                if (value != ClawControllerIndex)
                {
                    ManagerFactory.settingsManager.SetProperty("MSIClawControllerIndex", value);
                    OnPropertyChanged(nameof(ClawControllerIndex));
                }
            }
        }

        public DevicePageViewModel()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "BatteryChargeLimit":
                    BatteryChargeLimitPercent = Convert.ToBoolean(value) && CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryChargeLimitPercent);
                    break;
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            base.Dispose();
        }
    }
}
