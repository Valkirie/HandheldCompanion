using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using LibreHardwareMonitor.Hardware;
using System;
using System.Timers;

namespace HandheldCompanion.Platforms.Misc
{
    public class LibreHardwarePlatform : IPlatform
    {
        private Computer computer;

        private Timer updateTimer;
        private int updateInterval = 1000;

        // CPU
        private float? CPULoad;
        private float? CPUClock;
        private float? CPUPower;
        private float? CPUTemperature;

        // GPU
        private float? GPULoad;
        private float? GPUClock;
        private float? GPUPower;
        private float? GPUTemperature;
        private float? GPUMemory;
        private float? GPUMemoryDedicated;
        private float? GPUMemoryShared;
        private float? GPUMemoryTotal;
        private float? GPUMemoryDedicatedTotal;
        private float? GPUMemorySharedTotal;

        // MEMORY
        private float? MemoryUsage;
        private float? MemoryAvailable;

        // BATTERY
        private float? BatteryLevel;
        private float? BatteryPower;
        private float? BatteryTimeSpan;

        public LibreHardwarePlatform()
        {
            Name = "LibreHardwareMonitor";
            IsInstalled = true;

            // watchdog to populate sensors
            updateTimer = new Timer(updateInterval) { Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            // prepare for sensors reading
            computer = new Computer
            {
                IsCpuEnabled = IDevice.GetCurrent().CpuMonitor,
                IsGpuEnabled = IDevice.GetCurrent().GpuMonitor,
                IsMemoryEnabled = IDevice.GetCurrent().MemoryMonitor,
                IsBatteryEnabled = IDevice.GetCurrent().BatteryMonitor,
            };
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "OnScreenDisplayRefreshRate":
                    updateInterval = Convert.ToInt32(value);
                    updateTimer.Interval = updateInterval;
                    break;
            }
        }

        public override bool Start()
        {
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

            if (computer is not null)
            {
                // open computer, slow task
                computer.Open();

                // prevent sensor from being stored to memory for too long
                foreach (var hardware in computer.Hardware)
                    foreach (var sensor in hardware.Sensors)
                        sensor.ValuesTimeWindow = new(0, 0, 10);
            }

            updateTimer?.Start();

            return base.Start();
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("OnScreenDisplayRefreshRate", ManagerFactory.settingsManager.GetString("OnScreenDisplayRefreshRate"), false);
        }

        public override bool Stop(bool kill = false)
        {
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

            updateTimer?.Stop();

            // wait until all tasks are complete
            lock (updateLock)
            {
                computer?.Close();
            }

            return base.Stop(kill);
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            lock (updateLock)
            {
                // pull temperature sensor
                foreach (IHardware? hardware in computer.Hardware)
                {
                    try { hardware.Update(); } catch { }

                    switch (hardware.HardwareType)
                    {
                        case HardwareType.Cpu:
                            HandleCPU(hardware);
                            break;
                        case HardwareType.GpuNvidia:
                        case HardwareType.GpuAmd:
                        case HardwareType.GpuIntel:
                            HandleGPU(hardware);
                            break;
                        case HardwareType.Memory:
                            HandleMemory(hardware);
                            break;
                        case HardwareType.Battery:
                            HandleBattery(hardware);
                            break;
                    }
                }
            }
        }

        #region gpu updates
        public float? GetGPULoad() => computer?.IsGpuEnabled ?? false ? GPULoad : null;
        public float? GetGPUPower() => computer?.IsGpuEnabled ?? false ? GPUPower : null;
        public float? GetGPUTemperature() => computer?.IsGpuEnabled ?? false ? GPUTemperature : null;

        public float? GetGPUMemory() => computer?.IsGpuEnabled ?? false ? GPUMemory : null;
        public float? GetGPUMemoryDedicated() => computer?.IsGpuEnabled ?? false ? GPUMemoryDedicated : null;
        public float? GetGPUMemoryShared() => computer?.IsGpuEnabled ?? false ? GPUMemoryShared : null;

        public float? GetGPUMemoryTotal() => computer?.IsGpuEnabled ?? false ? GPUMemoryTotal : null;
        public float? GetGPUMemoryDedicatedTotal() => computer?.IsGpuEnabled ?? false ? GPUMemoryDedicatedTotal : null;
        public float? GetGPUMemorySharedTotal() => computer?.IsGpuEnabled ?? false ? GPUMemorySharedTotal : null;

        private void HandleGPU(IHardware gpu)
        {
            float highestClock = 0;
            foreach (var sensor in gpu.Sensors)
            {
                // May crash the app when Value is null, better to check first
                if (sensor.Value is null)
                    continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Load:
                        HandleGPU_Load(sensor);
                        break;
                    case SensorType.Clock:
                        highestClock = HandleGPU_Clock(sensor, highestClock);
                        break;
                    case SensorType.Power:
                        HandleGPU_Power(sensor);
                        break;
                    case SensorType.Temperature:
                        HandleGPU_Temperature(sensor);
                        break;
                    case SensorType.Data:
                    case SensorType.SmallData:
                        HandleGPU_Data(sensor);
                        break;
                }
            }
        }

        private void HandleGPU_Data(ISensor sensor)
        {
            if (sensor.Name == "GPU Memory Used")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemory != value)
                {
                    GPUMemory = value;
                    GPUMemoryChanged?.Invoke(GPUMemory);
                }
            }
            else if (sensor.Name == "D3D Dedicated Memory Used")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemoryDedicated != value)
                {
                    GPUMemoryDedicated = value;
                    GPUMemoryDedicatedChanged?.Invoke(GPUMemoryDedicated);
                }
            }
            else if (sensor.Name == "D3D Dedicated Memory Shared")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemoryShared != value)
                {
                    GPUMemoryShared = value;
                    GPUMemorySharedChanged?.Invoke(GPUMemoryShared);
                }
            }
            else if (sensor.Name == "GPU Memory Total")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemoryTotal != value)
                    GPUMemoryTotal = value;
            }
            else if (sensor.Name == "D3D Dedicated Memory Total")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemoryDedicatedTotal != value)
                    GPUMemoryDedicatedTotal = value;
            }
            else if (sensor.Name == "D3D Dedicated Memory Total")
            {
                float value = (float)sensor.Value / 1024.0f; // MB to GB
                if (GPUMemorySharedTotal != value)
                    GPUMemorySharedTotal = value;
            }
        }

        private void HandleGPU_Load(ISensor sensor)
        {
            if (sensor.Name == "D3D 3D")
            {
                float value = (float)sensor.Value;
                if (GPULoad != value)
                {
                    GPULoad = value;
                    GPULoadChanged?.Invoke(GPULoad);
                }
            }
        }

        private float HandleGPU_Clock(ISensor sensor, float currentHighest)
        {
            if (sensor.Name == "GPU Core")
            {
                float value = (float)sensor.Value;
                if (value > currentHighest)
                {
                    if (GPUClock != value)
                    {
                        GPUClock = value;
                        GPUClockChanged?.Invoke(GPUClock);
                        return value;
                    }
                }
            }
            return currentHighest;
        }

        private void HandleGPU_Power(ISensor sensor)
        {
            switch (sensor.Name)
            {
                case "GPU SoC":
                    //case "GPU Package":
                    {
                        float value = (float)sensor.Value;
                        if (GPUPower != value)
                        {
                            GPUPower = value;
                            GPUPowerChanged?.Invoke(GPUPower);
                        }
                    }
                    break;
            }
        }

        private void HandleGPU_Temperature(ISensor sensor)
        {
            if (sensor.Name == "GPU Core")
            {
                float value = (float)sensor.Value;
                if (GPUTemperature != value)
                {
                    GPUTemperature = value;
                    GPUTemperatureChanged?.Invoke(GPUTemperature);
                }
            }
        }
        #endregion

        #region cpu updates
        public float? GetCPULoad() => computer?.IsCpuEnabled ?? false ? CPULoad : null;
        public float? GetCPUPower() => computer?.IsCpuEnabled ?? false ? CPUPower : null;
        public float? GetCPUTemperature() => computer?.IsCpuEnabled ?? false ? CPUTemperature : null;

        private void HandleCPU(IHardware cpu)
        {
            float highestClock = 0;
            foreach (var sensor in cpu.Sensors)
            {
                // May crash the app when Value is null, better to check first
                if (!sensor.Value.HasValue || sensor.Value == 0)
                    continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Load:
                        HandleCPU_Load(sensor);
                        break;
                    case SensorType.Clock:
                        highestClock = HandleCPU_Clock(sensor, highestClock);
                        break;
                    case SensorType.Power:
                        HandleCPU_Power(sensor);
                        break;
                    case SensorType.Temperature:
                        HandleCPU_Temperature(sensor);
                        break;
                }
            }
        }

        private void HandleCPU_Load(ISensor sensor)
        {
            if (sensor.Name == "CPU Total")
            {
                float value = (float)sensor.Value;
                if (CPULoad != value)
                {
                    CPULoad = value;
                    CPULoadChanged?.Invoke(CPULoad);
                }
            }
        }

        private float HandleCPU_Clock(ISensor sensor, float currentHighest)
        {
            if (sensor.Name.StartsWith("CPU Core #") || sensor.Name.StartsWith("Core #"))
            {
                float value = (float)sensor.Value;
                if (value > currentHighest)
                {
                    if (CPUClock != value)
                    {
                        CPUClock = (float)sensor.Value;
                        CPUClockChanged?.Invoke(CPUClock);
                    }
                    return value;
                }
            }
            return currentHighest;
        }

        private void HandleCPU_Power(ISensor sensor)
        {
            switch (sensor.Name)
            {
                case "Package":
                case "CPU Package":
                    {
                        float value = (float)sensor.Value;
                        if (CPUPower != value)
                        {
                            CPUPower = value;
                            CPUPowerChanged?.Invoke(CPUPower);
                        }
                    }
                    break;
            }
        }

        private void HandleCPU_Temperature(ISensor sensor)
        {
            if (sensor.Name == "CPU Package" || sensor.Name == "Core (Tctl/Tdie)")
            {
                float value = (float)sensor.Value;
                if (CPUTemperature != value)
                {
                    CPUTemperature = value;
                    CPUTemperatureChanged?.Invoke(CPUTemperature);
                }
            }
        }
        #endregion

        #region memory updates
        public float? GetMemoryUsage() => computer?.IsMemoryEnabled ?? false ? MemoryUsage : null;
        public float? GetMemoryAvailable() => computer?.IsMemoryEnabled ?? false ? MemoryAvailable : null;
        public float? GetMemoryTotal() => GetMemoryUsage() + GetMemoryAvailable();

        private void HandleMemory(IHardware cpu)
        {
            foreach (var sensor in cpu.Sensors)
            {
                // May crash the app when Value is null, better to check first
                if (!sensor.Value.HasValue || sensor.Value == 0)
                    continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Data:
                    case SensorType.SmallData:
                        HandleMemory_Data(sensor);
                        break;
                }
            }
        }

        private void HandleMemory_Data(ISensor sensor)
        {
            if (sensor.Name == "Memory Used")
            {
                float value = (float)sensor.Value;
                if (MemoryUsage != value)
                {
                    MemoryUsage = value;
                    MemoryUsageChanged?.Invoke(MemoryUsage);
                }
            }
            else if (sensor.Name == "Memory Available")
            {
                float value = (float)sensor.Value;
                if (MemoryAvailable != value)
                {
                    MemoryAvailable = value;
                    MemoryAvailableChanged?.Invoke(MemoryAvailable);
                }
            }
        }
        #endregion

        #region battery updates
        public float? GetBatteryLevel() => computer?.IsBatteryEnabled ?? false ? BatteryLevel : null;
        public float? GetBatteryPower() => computer?.IsBatteryEnabled ?? false ? BatteryPower : null;
        public float? GetBatteryTimeSpan() => computer?.IsBatteryEnabled ?? false ? BatteryTimeSpan : null;

        private void HandleBattery(IHardware cpu)
        {
            foreach (var sensor in cpu.Sensors)
            {
                // May crash the app when Value is null, better to check first
                if (!sensor.Value.HasValue || sensor.Value == 0)
                    continue;

                switch (sensor.SensorType)
                {
                    case SensorType.Level:
                        HandleBattery_Level(sensor);
                        break;
                    case SensorType.Power:
                        HandleBattery_Power(sensor);
                        break;
                    case SensorType.TimeSpan:
                        HandleBattery_TimeSpan(sensor);
                        break;
                }
            }
        }

        private void HandleBattery_Level(ISensor sensor)
        {
            if (sensor.Name == "Charge Level")
            {
                float value = (float)sensor.Value;
                if (BatteryLevel != value)
                {
                    BatteryLevel = value;
                    BatteryLevelChanged?.Invoke(BatteryLevel);
                }
            }
        }

        private void HandleBattery_Power(ISensor sensor)
        {
            if (sensor.Name == "Charge Rate")
            {
                float value = (float)sensor.Value;
                if (BatteryPower != value)
                {
                    BatteryPower = value;
                    BatteryPowerChanged?.Invoke(BatteryPower);
                }
            }
            if (sensor.Name == "Discharge Rate")
            {
                float value = -(float)sensor.Value;
                if (BatteryPower != value)
                {
                    BatteryPower = value;
                    BatteryPowerChanged?.Invoke(BatteryPower);
                }
            }
        }

        private void HandleBattery_TimeSpan(ISensor sensor)
        {
            if (sensor.Name == "Remaining Time (Estimated)")
            {
                float value = (float)sensor.Value / 60.0f;
                if (BatteryTimeSpan != value)
                {
                    BatteryTimeSpan = value;
                    BatteryTimeSpanChanged?.Invoke(BatteryTimeSpan);
                }
            }
        }
        #endregion

        #region events
        public delegate void ChangedHandler(float? value);

        public event ChangedHandler CPULoadChanged;
        public event ChangedHandler CPUPowerChanged;
        public event ChangedHandler CPUClockChanged;
        public event ChangedHandler CPUTemperatureChanged;

        public event ChangedHandler GPULoadChanged;
        public event ChangedHandler GPUPowerChanged;
        public event ChangedHandler GPUClockChanged;
        public event ChangedHandler GPUTemperatureChanged;
        public event ChangedHandler GPUMemoryChanged;
        public event ChangedHandler GPUMemoryDedicatedChanged;
        public event ChangedHandler GPUMemorySharedChanged;

        public event ChangedHandler MemoryUsageChanged;
        public event ChangedHandler MemoryAvailableChanged;

        public event ChangedHandler BatteryLevelChanged;
        public event ChangedHandler BatteryPowerChanged;
        public event ChangedHandler BatteryTimeSpanChanged;
        #endregion
    }
}