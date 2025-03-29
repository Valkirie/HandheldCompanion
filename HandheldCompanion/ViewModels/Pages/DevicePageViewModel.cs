using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Watchers;
using System;
using System.Windows;

namespace HandheldCompanion.ViewModels
{
    public class DevicePageViewModel : BaseViewModel
    {
        public bool IsUnsupportedDevice => IDevice.GetCurrent() is DefaultDevice;
        private IDevice CurrentDevice => IDevice.GetCurrent();

        #region Battery bypass
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
        #endregion

        #region Power options
        public bool HasWMIMethod => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.WMIMethod);
        public int ConfigurableTDPMethod
        {
            get
            {
                return ManagerFactory.settingsManager.GetInt("ConfigurableTDPMethod");
            }
            set
            {
                if (value != ConfigurableTDPMethod)
                {
                    ManagerFactory.settingsManager.SetProperty("ConfigurableTDPMethod", value);
                    OnPropertyChanged(nameof(ConfigurableTDPMethod));
                }
            }
        }
        #endregion

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

        #region MemoryIntegrity
        private CoreIsolationWatcher coreIsolationWatcher = new CoreIsolationWatcher();
        public bool MemoryIntegrity
        {
            get
            {
                return coreIsolationWatcher.VulnerableDriverBlocklistEnable || coreIsolationWatcher.HypervisorEnforcedCodeIntegrityEnabled;
            }
            set
            {
                coreIsolationWatcher.SetSettings(value);
            }
        }
        #endregion

        #region Manufacturer application
        private ISpaceWatcher manufacturerWatcher;
        public bool ManufacturerApplication
        {
            get
            {
                return (manufacturerWatcher?.HasProcesses() ?? false) ||
                       (manufacturerWatcher?.HasEnabledTasks() ?? false) ||
                       (manufacturerWatcher?.HasRunningServices() ?? false);
            }
            set
            {
                if (value)
                {
                    manufacturerWatcher?.KillProcesses();
                    manufacturerWatcher?.DisableTasks();
                    manufacturerWatcher?.DisableServices();
                }
                else
                {
                    manufacturerWatcher?.EnableTasks();
                    manufacturerWatcher?.EnableServices();
                }
                OnPropertyChanged(nameof(ManufacturerApplication));
            }
        }
        #endregion

        public DevicePageViewModel()
        {
            // prepare watchers
            coreIsolationWatcher.Start();

            // manufacturer watcher
            if (IDevice.GetCurrent() is ClawA1M || IDevice.GetCurrent() is Claw8)
                manufacturerWatcher = new ClawCenterWatcher();

            manufacturerWatcher?.Start();

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            coreIsolationWatcher.KeyChanged += CoreIsolationWatcher_KeyChanged;
        }

        private void CoreIsolationWatcher_KeyChanged(string key, bool enabled)
        {
            OnPropertyChanged(nameof(MemoryIntegrity));
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
            coreIsolationWatcher.Stop();
            manufacturerWatcher?.Stop();

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            coreIsolationWatcher.KeyChanged -= CoreIsolationWatcher_KeyChanged;

            base.Dispose();
        }
    }
}