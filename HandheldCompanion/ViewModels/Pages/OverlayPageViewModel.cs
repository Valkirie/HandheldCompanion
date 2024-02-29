using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System;

namespace HandheldCompanion.ViewModels
{
    public class OverlayPageViewModel : BaseViewModel
    {
        public bool IsRunningRTSS => PlatformManager.RTSS.IsInstalled;

        public bool IsRunningLHM => PlatformManager.LibreHardwareMonitor.IsInstalled;

        private int _onScreenDisplayLevel;
        public int OnScreenDisplayLevel
        {
            get => _onScreenDisplayLevel;
            set
            {
                if (value != OnScreenDisplayLevel)
                {
                    _onScreenDisplayLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayLevel));
                }
            }
        }

        private int _onScreenDisplayTimeLevel;
        public int OnScreenDisplayTimeLevel
        {
            get => _onScreenDisplayTimeLevel;
            set
            {
                if (value != OnScreenDisplayTimeLevel)
                {
                    _onScreenDisplayTimeLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayTimeLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayTimeLevel));
                }
            }
        }

        private int _onScreenDisplayFPSLevel;
        public int OnScreenDisplayFPSLevel
        {
            get => _onScreenDisplayFPSLevel;
            set
            {
                if (value != OnScreenDisplayFPSLevel)
                {
                    _onScreenDisplayFPSLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayFPSLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayFPSLevel));
                }
            }
        }

        private int _onScreenDisplayCPULevel;
        public int OnScreenDisplayCPULevel
        {
            get => _onScreenDisplayCPULevel;
            set
            {
                if (value != OnScreenDisplayCPULevel)
                {
                    _onScreenDisplayCPULevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayCPULevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayCPULevel));
                }
            }
        }

        private int _onScreenDisplayGPULevel;
        public int OnScreenDisplayGPULevel
        {
            get => _onScreenDisplayGPULevel;
            set
            {
                if (value != OnScreenDisplayGPULevel)
                {
                    _onScreenDisplayGPULevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayGPULevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayGPULevel));
                }
            }
        }

        private int _onScreenDisplayRAMLevel;
        public int OnScreenDisplayRAMLevel
        {
            get => _onScreenDisplayRAMLevel;
            set
            {
                if (value != OnScreenDisplayRAMLevel)
                {
                    _onScreenDisplayRAMLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayRAMLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayRAMLevel));
                }
            }
        }

        private int _onScreenDisplayVRAMLevel;
        public int OnScreenDisplayVRAMLevel
        {
            get => _onScreenDisplayVRAMLevel;
            set
            {
                if (value != OnScreenDisplayVRAMLevel)
                {
                    _onScreenDisplayVRAMLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayVRAMLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayVRAMLevel));
                }
            }
        }

        private int _onScreenDisplayBATTLevel;
        public int OnScreenDisplayBATTLevel
        {
            get => _onScreenDisplayBATTLevel;
            set
            {
                if (value != OnScreenDisplayBATTLevel)
                {
                    _onScreenDisplayBATTLevel = value;
                    SettingsManager.SetProperty(Settings.OnScreenDisplayBATTLevel, value);
                    OnPropertyChanged(nameof(OnScreenDisplayBATTLevel));
                }
            }
        }

        public OverlayPageViewModel()
        {
            _onScreenDisplayLevel = SettingsManager.GetInt(Settings.OnScreenDisplayLevel);
            _onScreenDisplayTimeLevel = SettingsManager.GetInt(Settings.OnScreenDisplayTimeLevel);
            _onScreenDisplayFPSLevel = SettingsManager.GetInt(Settings.OnScreenDisplayFPSLevel);
            _onScreenDisplayCPULevel = SettingsManager.GetInt(Settings.OnScreenDisplayCPULevel);
            _onScreenDisplayGPULevel = SettingsManager.GetInt(Settings.OnScreenDisplayGPULevel);
            _onScreenDisplayRAMLevel = SettingsManager.GetInt(Settings.OnScreenDisplayRAMLevel);
            _onScreenDisplayVRAMLevel = SettingsManager.GetInt(Settings.OnScreenDisplayVRAMLevel);
            _onScreenDisplayBATTLevel = SettingsManager.GetInt(Settings.OnScreenDisplayBATTLevel);

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated += PlatformManager_RTSS_Updated;
        }

        public override void Dispose()
        {
            SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated -= PlatformManager_RTSS_Updated;
            base.Dispose();
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            UpdateSettings(name, value);
            OnPropertyChanged(name); // setting names matches property name
        }

        private void PlatformManager_RTSS_Updated(PlatformStatus status)
        {
            if (status == Platforms.PlatformStatus.Stalled)
                OnScreenDisplayLevel = 0;
        }

        private void UpdateSettings(string name, object value)
        {
            if (name == Settings.OnScreenDisplayLevel)
                _onScreenDisplayLevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayTimeLevel)
                _onScreenDisplayTimeLevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayFPSLevel)
                _onScreenDisplayFPSLevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayCPULevel)
                _onScreenDisplayCPULevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayGPULevel)
                _onScreenDisplayGPULevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayRAMLevel)
                _onScreenDisplayRAMLevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayVRAMLevel)
                _onScreenDisplayVRAMLevel = Convert.ToInt32(value);

            else if (name == Settings.OnScreenDisplayBATTLevel)
                _onScreenDisplayBATTLevel = Convert.ToInt32(value);
        }
    }
}
