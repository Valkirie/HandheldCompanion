using HandheldCompanion.Devices;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using LiveCharts;
using System;
using System.Timers;
using System.Windows.Media;

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
                    OnPropertyChanged(nameof(OnScreenDisplayLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayLevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayTimeLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayTimeLevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayFPSLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayFPSLevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayCPULevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayCPULevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayGPULevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayGPULevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayRAMLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayRAMLevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayVRAMLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayVRAMLevel, value);
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
                    OnPropertyChanged(nameof(OnScreenDisplayBATTLevel));

                    ManagerFactory.settingsManager.SetProperty(Settings.OnScreenDisplayBATTLevel, value);
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

        private ChartValues<double> _framerateValues = [];
        public ChartValues<double> FramerateValues
        {
            get { return _framerateValues; }
            set
            {
                if (value != _framerateValues)
                {
                    _framerateValues = value;
                    OnPropertyChanged(nameof(FramerateValues));
                }
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

            _onScreenDisplayLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayLevel);
            _onScreenDisplayTimeLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayTimeLevel);
            _onScreenDisplayFPSLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayFPSLevel);
            _onScreenDisplayCPULevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayCPULevel);
            _onScreenDisplayGPULevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayGPULevel);
            _onScreenDisplayRAMLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayRAMLevel);
            _onScreenDisplayVRAMLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayVRAMLevel);
            _onScreenDisplayBATTLevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayBATTLevel);

            CPUName = IDevice.GetCurrent().Processor;

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated += PlatformManager_RTSS_Updated;

            if (IDevice.GetCurrent().CpuMonitor)
            {
                PlatformManager.LibreHardwareMonitor.CPUPowerChanged += LibreHardwareMonitor_CPUPowerChanged;
                PlatformManager.LibreHardwareMonitor.CPUTemperatureChanged += LibreHardwareMonitor_CPUTemperatureChanged;
                PlatformManager.LibreHardwareMonitor.CPULoadChanged += LibreHardwareMonitor_CPULoadChanged;
            }

            ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            ManagerFactory.gpuManager.Hooked += GPUManager_Hooked;

            // raise events
            switch (ManagerFactory.processManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryForeground();
                    break;
            }

            switch (ManagerFactory.gpuManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.gpuManager.Initialized += GpuManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryGPU();
                    break;
            }
        }

        private void QueryGPU()
        {
            GPU gpu = GPUManager.GetCurrent();
            if (gpu is not null)
                GPUManager_Hooked(gpu);
        }

        private void GpuManager_Initialized()
        {
            QueryGPU();
        }

        private void QueryForeground()
        {
            ProcessManager_ForegroundChanged(ProcessManager.GetForegroundProcess(), null);
        }

        private void ProcessManager_Initialized()
        {
            QueryForeground();
        }

        private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx)
        {
            // get path
            string path = processEx != null ? processEx.Path : string.Empty;

            UIHelper.TryInvoke(() =>
            {
                ProcessIcon = processEx?.ProcessIcon;

                if (processEx is null)
                {
                    ProcessName = Properties.Resources.QuickProfilesPage_Waiting;
                    ProcessPath = string.Empty;
                }
                else
                {
                    ProcessName = processEx.Executable;
                    ProcessPath = processEx.Path;
                }
            });
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            GPU gpu = GPUManager.GetCurrent();
            if (gpu is not null)
            {
                if (gpu.HasPower())
                    GPUPower = (float)Math.Round((float)gpu.GetPower());

                if (gpu.HasLoad())
                    GPULoad = (float)Math.Round((float)gpu.GetLoad());

                if (gpu.HasTemperature())
                    GPUTemperature = (float)Math.Round((float)gpu.GetTemperature());
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

            HasGPUPower = GPU is not null && GPU.HasPower();
            HasGPUTemperature = GPU is not null && GPU.HasTemperature();
            HasGPULoad = GPU is not null && GPU.HasLoad();

            if (IDevice.GetCurrent().GpuMonitor)
            {
                if (!HasGPUPower) PlatformManager.LibreHardwareMonitor.GPUPowerChanged += LibreHardwareMonitor_GPUPowerChanged;
                if (!HasGPUTemperature) PlatformManager.LibreHardwareMonitor.GPUTemperatureChanged += LibreHardwareMonitor_GPUTemperatureChanged;
                if (!HasGPULoad) PlatformManager.LibreHardwareMonitor.GPULoadChanged += LibreHardwareMonitor_GPULoadChanged;
            }
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

        private void LibreHardwareMonitor_GPULoadChanged(float? value)
        {
            if (value is null)
                return;

            // todo: improve me
            if (!HasGPULoad)
                HasGPULoad = value != 0.0f;

            GPULoad = (float)Math.Round((float)value);
        }

        private void LibreHardwareMonitor_GPUTemperatureChanged(float? value)
        {
            if (value is null)
                return;

            // todo: improve me
            if (!HasGPUTemperature)
                HasGPUTemperature = value != 0.0f;

            GPUTemperature = (float)Math.Round((float)value);
        }

        private void LibreHardwareMonitor_GPUPowerChanged(float? value)
        {
            if (value is null)
                return;

            // todo: improve me
            if (!HasGPUPower)
                HasGPUPower = value != 0.0f;

            GPUPower = (float)Math.Round((float)value);
        }

        public override void Dispose()
        {
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            PlatformManager.RTSS.Updated -= PlatformManager_RTSS_Updated;
            base.Dispose();
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
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
