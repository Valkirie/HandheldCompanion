using HandheldCompanion.Controls;
using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using LiveCharts;
using RTSSSharedMemoryNET;
using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

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

        private float _CPUPower;
        public float CPUPower
        {
            get => _CPUPower;
            set
            {
                if (value != CPUPower)
                {
                    _CPUPower = value;
                    OnPropertyChanged(nameof(CPUPower));
                }
            }
        }

        private float _CPUTemperature;
        public float CPUTemperature
        {
            get => _CPUTemperature;
            set
            {
                if (value != CPUTemperature)
                {
                    _CPUTemperature = value;
                    OnPropertyChanged(nameof(CPUTemperature));
                }
            }
        }

        private float _CPULoad;
        public float CPULoad
        {
            get => _CPULoad;
            set
            {
                if (value != CPULoad)
                {
                    _CPULoad = value;
                    OnPropertyChanged(nameof(CPULoad));
                }
            }
        }

        // localize me
        private string _CPUName = "No CPU detected";
        public string CPUName
        {
            get => _CPUName;
            set
            {
                if (value != CPUName)
                {
                    _CPUName = value;
                    OnPropertyChanged(nameof(CPUName));
                }
            }
        }

        // localize me
        private string _GPUName = "No GPU detected";
        public string GPUName
        {
            get => _GPUName;
            set
            {
                if (value != GPUName)
                {
                    _GPUName = value;
                    OnPropertyChanged(nameof(GPUName));
                }
            }
        }

        private bool _HasGPUPower;
        public bool HasGPUPower
        {
            get => _HasGPUPower;
            set
            {
                if (value != HasGPUPower)
                {
                    _HasGPUPower = value;
                    OnPropertyChanged(nameof(HasGPUPower));
                }
            }
        }

        private float _GPUPower;
        public float GPUPower
        {
            get => _GPUPower;
            set
            {
                if (value != GPUPower)
                {
                    _GPUPower = value;
                    OnPropertyChanged(nameof(GPUPower));
                }
            }
        }

        private bool _HasGPUTemperature;
        public bool HasGPUTemperature
        {
            get => _HasGPUTemperature;
            set
            {
                if (value != HasGPUTemperature)
                {
                    _HasGPUTemperature = value;
                    OnPropertyChanged(nameof(HasGPUTemperature));
                }
            }
        }

        private float _GPUTemperature;
        public float GPUTemperature
        {
            get => _GPUTemperature;
            set
            {
                if (value != GPUTemperature)
                {
                    _GPUTemperature = value;
                    OnPropertyChanged(nameof(GPUTemperature));
                }
            }
        }

        private bool _HasGPULoad;
        public bool HasGPULoad
        {
            get => _HasGPULoad;
            set
            {
                if (value != HasGPULoad)
                {
                    _HasGPULoad = value;
                    OnPropertyChanged(nameof(HasGPULoad));
                }
            }
        }

        private float _GPULoad;
        public float GPULoad
        {
            get => _GPULoad;
            set
            {
                if (value != GPULoad)
                {
                    _GPULoad = value;
                    OnPropertyChanged(nameof(GPULoad));
                }
            }
        }

        private double _Framerate;
        public double Framerate
        {
            get => _Framerate;
            set
            {
                if (value != Framerate)
                {
                    _Framerate = value;
                    OnPropertyChanged(nameof(Framerate));
                }
            }
        }

        private double _Frametime;
        public double Frametime
        {
            get => _Frametime;
            set
            {
                if (value != Frametime)
                {
                    _Frametime = value;
                    OnPropertyChanged(nameof(Frametime));
                }
            }
        }

        private ChartValues<double> _framerateValues = new();
        public ChartValues<double> FramerateValues
        {
            get { return _framerateValues; }
            set
            {
                _framerateValues = value;
                OnPropertyChanged(nameof(FramerateValues));
            }
        }

        private ImageSource _ProcessIcon;
        public ImageSource ProcessIcon
        {
            get => _ProcessIcon;
            set
            {
                if (value != ProcessIcon)
                {
                    _ProcessIcon = value;
                    OnPropertyChanged(nameof(ProcessIcon));
                }
            }
        }

        private string _ProcessName = Properties.Resources.QuickProfilesPage_Waiting;
        public string ProcessName
        {
            get => _ProcessName;
            set
            {
                if (value != ProcessName)
                {
                    _ProcessName = value;
                    OnPropertyChanged(nameof(ProcessName));
                }
            }
        }

        private string _ProcessPath;
        public string ProcessPath
        {
            get => _ProcessPath;
            set
            {
                if (value != ProcessPath)
                {
                    _ProcessPath = value;
                    OnPropertyChanged(nameof(ProcessPath));
                }
            }
        }

        private Timer updateTimer;
        private int updateInterval = 1000;

        private Timer framerateTimer;
        private int framerateInterval = 1000;

        public OverlayPageViewModel()
        {
            updateTimer = new Timer(updateInterval) { Enabled = true };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            framerateTimer = new Timer(framerateInterval) { Enabled = true };
            framerateTimer.Elapsed += FramerateTimer_Elapsed;

            _onScreenDisplayLevel = SettingsManager.GetInt(Settings.OnScreenDisplayLevel);
            _onScreenDisplayTimeLevel = SettingsManager.GetInt(Settings.OnScreenDisplayTimeLevel);
            _onScreenDisplayFPSLevel = SettingsManager.GetInt(Settings.OnScreenDisplayFPSLevel);
            _onScreenDisplayCPULevel = SettingsManager.GetInt(Settings.OnScreenDisplayCPULevel);
            _onScreenDisplayGPULevel = SettingsManager.GetInt(Settings.OnScreenDisplayGPULevel);
            _onScreenDisplayRAMLevel = SettingsManager.GetInt(Settings.OnScreenDisplayRAMLevel);
            _onScreenDisplayVRAMLevel = SettingsManager.GetInt(Settings.OnScreenDisplayVRAMLevel);
            _onScreenDisplayBATTLevel = SettingsManager.GetInt(Settings.OnScreenDisplayBATTLevel);

            CPUName = IDevice.GetCurrent().Processor;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated += PlatformManager_RTSS_Updated;
            PlatformManager.LibreHardwareMonitor.CPUPowerChanged += LibreHardwareMonitor_CPUPowerChanged;
            PlatformManager.LibreHardwareMonitor.CPUTemperatureChanged += LibreHardwareMonitor_CPUTemperatureChanged;
            PlatformManager.LibreHardwareMonitor.CPULoadChanged += LibreHardwareMonitor_CPULoadChanged;
            GPUManager.Hooked += GPUManager_Hooked;
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            // GPUManager is a synchronous manager, it started before this page was loaded, force raise an event
            if (GPUManager.IsInitialized && GPUManager.GetCurrent() is not null)
                GPUManager_Hooked(GPUManager.GetCurrent());
        }

        private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx)
        {
            // get path
            string path = processEx != null ? processEx.Path : string.Empty;
            nint handle = processEx != null ? processEx.MainWindowHandle : IntPtr.Zero;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProcessIcon = processEx != null ? processEx.ProcessIcon : null;

                if (handle != IntPtr.Zero)
                {
                    ProcessName = processEx.Executable;
                    ProcessPath = processEx.Path;
                }
                else
                {
                    ProcessName = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath = string.Empty;
                }
            });
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            GPU gpu = GPUManager.GetCurrent();
            if (gpu is not null)
            {
                if (gpu.HasPower())
                {
                    GPUPower = (float)Math.Round((float)gpu.GetPower());
                }

                if (gpu.HasLoad())
                {
                    GPULoad = (float)Math.Round((float)gpu.GetLoad());
                }

                if (gpu.HasTemperature())
                {
                    GPUTemperature = (float)Math.Round((float)gpu.GetTemperature());
                }
            }
        }

        private void FramerateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (PlatformManager.RTSS.HasHook())
            {
                PlatformManager.RTSS.RefreshAppEntry();

                // refresh values
                Framerate = Math.Round(PlatformManager.RTSS.GetFramerate());
                Frametime = Math.Round(PlatformManager.RTSS.GetFrametime(), 1);

                /*
                if (FramerateValues.Count == 100)
                    FramerateValues.RemoveAt(0);

                FramerateValues.Add(framerate);
                */
            }
            else
            {
                Framerate = 0;
                Frametime = 0;

                /*
                if (FramerateValues.Count != 0)
                    FramerateValues = new();
                */
            }
        }

        private void GPUManager_Hooked(GPU GPU)
        {
            // localize me
            GPUName = GPU is not null ? GPU.adapterInformation.Details.Description : "No GPU detected";

            HasGPUPower = GPU is not null ? GPU.HasPower() : false;
            HasGPUTemperature = GPU is not null ? GPU.HasTemperature() : false;
            HasGPULoad = GPU is not null ? GPU.HasLoad() : false;
        }

        private void LibreHardwareMonitor_CPULoadChanged(float? value)
        {
            if (value is null)
                return;

            CPULoad = (float)Math.Round((float)value);
        }

        private void LibreHardwareMonitor_CPUTemperatureChanged(float? value)
        {
            if (value is null)
                return;

            CPUTemperature = (float)Math.Round((float)value);
        }

        private void LibreHardwareMonitor_CPUPowerChanged(float? value)
        {
            if (value is null)
                return;

            CPUPower = (float)Math.Round((float)value);
        }

        public override void Dispose()
        {
            SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated -= PlatformManager_RTSS_Updated;
            base.Dispose();
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "OnScreenDisplayRefreshRate":
                    updateInterval = Convert.ToInt32(value);
                    updateTimer.Interval = updateInterval;

                    framerateInterval = Convert.ToInt32(value);
                    framerateTimer.Interval = framerateInterval;
                    return;
            }

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
