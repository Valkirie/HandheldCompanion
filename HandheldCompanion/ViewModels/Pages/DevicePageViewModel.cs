using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Zotac;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Views;
using HandheldCompanion.Watchers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Threading.Tasks;
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
        public int BatteryBypassStep => CurrentDevice.BatteryBypassStep;
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
        public bool HasWMIMethod => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.OEMCPU);
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

        public bool HasOverBoost => CurrentDevice is ClawA1M clawA1M && clawA1M.HasOverBoost();

        public bool ClawOverBoost
        {
            get
            {
                return CurrentDevice is ClawA1M clawA1M && clawA1M.GetOverBoost();
            }
            set
            {
                if (value != ClawOverBoost)
                {
                    if (CurrentDevice is ClawA1M clawA1M)
                        clawA1M.SetOverBoost(value);

                    OnPropertyChanged(nameof(ClawOverBoost));
                }
            }
        }

        public double leftTriggerDeadzoneValue = 100;
        public double LeftTriggerDeadzoneValue
        {
            get
            {
                return leftTriggerDeadzoneValue;
            }
            set
            {
                if (value != leftTriggerDeadzoneValue)
                {
                    leftTriggerDeadzoneValue = value;
                    OnPropertyChanged(nameof(LeftTriggerDeadzoneValue));
                }
            }
        }

        public double rightTriggerDeadzoneValue = 100;
        public double RightTriggerDeadzoneValue
        {
            get
            {
                return rightTriggerDeadzoneValue;
            }
            set
            {
                if (value != rightTriggerDeadzoneValue)
                {
                    rightTriggerDeadzoneValue = value;
                    OnPropertyChanged(nameof(RightTriggerDeadzoneValue));
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

        private bool _ManufacturerAppBusy;
        public bool ManufacturerAppBusy
        {
            get
            {
                return !_ManufacturerAppBusy;
            }
            set
            {
                if (value != _ManufacturerAppBusy)
                {
                    _ManufacturerAppBusy = value;
                    OnPropertyChanged(nameof(ManufacturerAppBusy));
                }
            }
        }

        public bool ManufacturerAppStatus
        {
            get
            {
                return (manufacturerWatcher?.HasProcesses() ?? false) ||
                       (manufacturerWatcher?.HasEnabledTasks() ?? false) ||
                       (manufacturerWatcher?.HasRunningServices() ?? false);
            }
            set
            {
                // update flag
                ManufacturerAppBusy = true;

                if (value)
                {
                    manufacturerWatcher?.Enable();
                }
                else
                {
                    manufacturerWatcher?.Disable();
                }
            }
        }

        public bool HasManufacturerPlatform => manufacturerWatcher is not null;
        #endregion

        #region AdvancedSettings
        public bool HasCoreCurve => PerformanceManager.GetProcessor() is AMDProcessor AMD && AMD.HasAllCoreCurve && HasAdvancedSettings;
        public bool HasGPUCurve => PerformanceManager.GetProcessor() is AMDProcessor AMD && AMD.HasGpuCurve && HasAdvancedSettings;
        public bool IsIntel => PerformanceManager.GetProcessor() is IntelProcessor;
        public bool IsAMD => PerformanceManager.GetProcessor() is AMDProcessor;

        public bool HasAdvancedSettings
        {
            get
            {
                return ManagerFactory.settingsManager.GetBoolean("ConfigurableTDPOverride");
            }
            set
            {
                if (value != HasAdvancedSettings)
                    SetHasAdvancedSettingsAsync(value);
            }
        }

        private async Task SetHasAdvancedSettingsAsync(bool value)
        {
            if (value)
            {
                ContentDialogResult result = await new Dialog(MainWindow.GetCurrent())
                {
                    Title = "Warning",
                    Content = "Altering CPU power or voltage values might cause instabilities. Product warranties may not apply if the processor is operated beyond its specifications. Use at your own risk.",
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_OK
                }.ShowAsync();

                if (result != ContentDialogResult.Primary)
                    return;
            }

            ManagerFactory.settingsManager.SetProperty("ConfigurableTDPOverride", value);
            OnPropertyChanged(nameof(HasAdvancedSettings));
        }
        #endregion

        public DevicePageViewModel()
        {
            // settings watcher
            if (PerformanceManager.GetProcessor() is IntelProcessor)
            {
                coreIsolationWatcher.StatusChanged += CoreIsolationWatcher_StatusChanged;
                coreIsolationWatcher.Start();
            }

            // manufacturer watcher
            IDevice device = IDevice.GetCurrent();
            if (device is ClawA1M || device is ClawA2VM)
                manufacturerWatcher = new ClawCenterWatcher();
            else if (device is LegionGo)
                manufacturerWatcher = new LegionSpaceWatcher();
            else if (device is ROGAlly || device is ROGAllyX)
                manufacturerWatcher = new RogAllySpaceWatcher();
            else if (device is GamingZone)
                manufacturerWatcher = new ZotacLauncherWatcher();

            if (manufacturerWatcher is not null)
            {
                // start watcher
                manufacturerWatcher.StatusChanged += ManufacturerWatcher_StatusChanged;
                manufacturerWatcher.Start();
            }

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void CoreIsolationWatcher_StatusChanged(bool enabled)
        {
            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(coreIsolationWatcher.notification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(coreIsolationWatcher.notification);
                    break;
            }

            OnPropertyChanged(nameof(MemoryIntegrity));
        }

        private void ManufacturerWatcher_StatusChanged(bool enabled)
        {
            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(manufacturerWatcher.notification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(manufacturerWatcher.notification);
                    break;
            }

            // update flag
            ManufacturerAppBusy = false;
            OnPropertyChanged(nameof(ManufacturerAppStatus));
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
            coreIsolationWatcher.StatusChanged -= CoreIsolationWatcher_StatusChanged;
            coreIsolationWatcher.Stop();

            if (manufacturerWatcher is not null)
            {
                manufacturerWatcher.StatusChanged -= ManufacturerWatcher_StatusChanged;
                manufacturerWatcher.Stop();
            }

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            base.Dispose();
        }
    }
}