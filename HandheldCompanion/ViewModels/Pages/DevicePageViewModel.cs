using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
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
        public Visibility BatteryBypassVisibility => BatteryChargeLimitCapacity ? Visibility.Visible : Visibility.Collapsed;
        public Visibility BatteryBypassModeVisibility => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryBypassCharging) ? Visibility.Visible : Visibility.Collapsed;
        public bool BatteryChargeLimitCapacity => CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.BatteryChargeLimit);

        private bool _BatteryChargeLimit;
        public bool BatteryChargeLimit
        {
            get
            {
                return _BatteryChargeLimit;
            }
            set
            {
                if (value != _BatteryChargeLimit)
                {
                    _BatteryChargeLimit = value;
                    OnPropertyChanged(nameof(BatteryChargeLimit));
                }
            }
        }

        private double _BatteryChargeLimitPercent = 100.0d;
        public double BatteryChargeLimitPercent
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

        private bool _GoBackToSleep;
        public bool GoBackToSleep
        {
            get => _GoBackToSleep;
            set
            {
                if (value != _GoBackToSleep)
                {
                    _GoBackToSleep = value;
                    OnPropertyChanged(nameof(GoBackToSleep));
                }
            }
        }
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

        #region MemoryIntegrity
        private CoreIsolationWatcher coreIsolationWatcher = new CoreIsolationWatcher();
        public bool MemoryIntegrity
        {
            get
            {
                return coreIsolationWatcher.VulnerableDriverBlocklistEnable || coreIsolationWatcher.HypervisorEnforcedCodeIntegrityEnabled || coreIsolationWatcher.SmartAppControlEnabled;
            }
            set
            {
                coreIsolationWatcher.SetSettings(value, MainWindow.GetCurrent());
            }
        }
        #endregion

        #region Manufacturer application
        private ISpaceWatcher? manufacturerWatcher;

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
                return manufacturerWatcher?.IsRunning ?? false;
            }
            set
            {
                // update flag
                ManufacturerAppBusy = true;

                _ = Task.Run(async () =>
                {
                    // Enable or disable the manufacturer software
                    if (value)
                        manufacturerWatcher?.Enable();
                    else
                        manufacturerWatcher?.Disable();
                });
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
            // raise events
            PerformanceManager.Initialized += PerformanceManager_Initialized;
            if (PerformanceManager.GetProcessor() is not null)
                QueryProcessor();

            // manufacturer watcher
            manufacturerWatcher = ISpaceWatcher.CreateCurrent();

            if (manufacturerWatcher is not null)
            {
                // start watcher
                manufacturerWatcher.StatusChanged += ManufacturerWatcher_StatusChanged;
                manufacturerWatcher.Start();
            }

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }
        }

        private void PerformanceManager_Initialized(bool canChangeTDP, bool canChangeGPU)
        {
            QueryProcessor();
        }

        private void QueryProcessor()
        {
            if (PerformanceManager.GetProcessor() is IntelProcessor)
            {
                coreIsolationWatcher.StatusChanged += CoreIsolationWatcher_StatusChanged;
                coreIsolationWatcher.Start();
            }

            OnPropertyChanged(nameof(IsIntel));
            OnPropertyChanged(nameof(IsAMD));
            OnPropertyChanged(nameof(HasCoreCurve));
            OnPropertyChanged(nameof(HasGPUCurve));
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            SettingsManager_SettingValueChanged("BatteryChargeLimit", ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit"), false, false);
            SettingsManager_SettingValueChanged("BatteryChargeLimitPercent", ManagerFactory.settingsManager.GetDouble("BatteryChargeLimitPercent"), false, false);
            SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetBoolean("GoBackToSleep"), false, false);
        }

        private void CoreIsolationWatcher_StatusChanged(bool enabled)
        {
            var notification = coreIsolationWatcher.notification;
            if (notification is null)
                return;

            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(notification);
                    break;
                case false:
                    ManagerFactory.notificationManager.Discard(notification);
                    break;
            }

            OnPropertyChanged(nameof(MemoryIntegrity));
        }

        private void ManufacturerWatcher_StatusChanged(bool enabled)
        {
            var notification = manufacturerWatcher?.notification;
            if (notification is null)
                return;

            switch (enabled)
            {
                case true:
                    ManagerFactory.notificationManager.Add(notification);
                    break;
                case false:
                    {
                        ManagerFactory.notificationManager.Discard(notification);

                        // UI thread
                        _ = UIHelper.TryInvoke(async () =>
                        {
                            Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                            {
                                Title = Properties.Resources.Dialog_ForceRestartTitle,
                                Content = Properties.Resources.Dialog_ForceRestartDesc,
                                DefaultButton = ContentDialogButton.Close,
                                CloseButtonText = Properties.Resources.Dialog_No,
                                PrimaryButtonText = Properties.Resources.Dialog_Yes
                            }.ShowAsync();

                            await dialogTask; // sync call

                            switch (dialogTask.Result)
                            {
                                case ContentDialogResult.Primary:
                                    DeviceUtils.RestartComputer();
                                    break;
                                case ContentDialogResult.Secondary:
                                    break;
                            }
                        });
                    }
                    break;
            }

            // update flag
            ManufacturerAppBusy = false;
            OnPropertyChanged(nameof(ManufacturerAppStatus));
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "BatteryChargeLimit":
                    BatteryChargeLimit = Convert.ToBoolean(value);
                    break;
                case "BatteryChargeLimitPercent":
                    BatteryChargeLimitPercent = Convert.ToDouble(value);
                    break;
                case "GoBackToSleep":
                    GoBackToSleep = Convert.ToBoolean(value);
                    break;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                PerformanceManager.Initialized -= PerformanceManager_Initialized;

                coreIsolationWatcher.StatusChanged -= CoreIsolationWatcher_StatusChanged;
                coreIsolationWatcher.Stop();
                coreIsolationWatcher.Dispose();

                if (manufacturerWatcher is not null)
                {
                    manufacturerWatcher.StatusChanged -= ManufacturerWatcher_StatusChanged;
                    manufacturerWatcher.Stop();
                    manufacturerWatcher.Dispose();
                }

                // manage events
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            }

            base.Dispose(disposing);
        }
    }
}