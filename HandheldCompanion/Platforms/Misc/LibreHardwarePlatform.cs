using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using LibreHardwareMonitor.Hardware;
using System;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Platforms.Misc
{
    public class LibreHardwarePlatform : IPlatform
    {
        private const int MinimumCpuPollingInterval = 500;
        private const int MinimumGpuPollingInterval = 500;
        private const int MinimumMemoryPollingInterval = 2000;
        private const int MinimumBatteryPollingInterval = 5000;

        private Computer computer;
        private bool computerOpened;

        private Timer updateTimer;
        private int updateInterval = 1000;
        private long lastCpuUpdateTick;
        private long lastGpuUpdateTick;
        private long lastMemoryUpdateTick;
        private long lastBatteryUpdateTick;

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

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
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
                try
                {
                    computer.Open();
                    computerOpened = true;
                }
                catch (Exception ex)
                {
                    LogManager.LogError("LibreHardwareMonitor computer.Open() failed, {0}", ex.Message);
                    computerOpened = false;
                }

                // prevent sensor from being stored to memory for too long
                var window = new TimeSpan(0, 0, 10);
                foreach (var hardware in computer.Hardware)
                    ApplyValuesTimeWindow(hardware, window);
            }

            updateTimer?.Start();

            return base.Start();
        }

        private static void ApplyValuesTimeWindow(IHardware hardware, TimeSpan window)
        {
            foreach (var sensor in hardware.Sensors)
                sensor.ValuesTimeWindow = window;
            foreach (var subHardware in hardware.SubHardware)
                ApplyValuesTimeWindow(subHardware, window);
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
            SettingsManager_SettingValueChanged("OnScreenDisplayRefreshRate", ManagerFactory.settingsManager.GetString("OnScreenDisplayRefreshRate"), false, false);
        }

        public override bool Stop(bool kill = false)
        {
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

            updateTimer?.Stop();

            // Wait for any ongoing update to complete, with timeout to prevent deadlock
            // if UpdateTimer_Elapsed is blocked inside hardware.Update()
            const int LockTimeoutMs = 3000;
            if (Monitor.TryEnter(updateLock, LockTimeoutMs))
            {
                try
                {
                    computerOpened = false;
                    try { computer.Close(); } catch { }
                }
                finally
                {
                    Monitor.Exit(updateLock);
                }
            }
            else
            {
                // Could not acquire lock within timeout; log and continue
                // The update thread may be blocked in hardware.Update()
                LogManager.LogWarning("LibreHardwarePlatform.Stop() could not acquire updateLock within {0}ms; proceeding without cleanup", LockTimeoutMs);
                computerOpened = false;
            }

            return base.Stop(kill);
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (!computerOpened || computer is null)
                return;

            lock (updateLock)
            {
                long now = Environment.TickCount64;
                bool shouldUpdateCpu = ShouldUpdateHardware(now, ref lastCpuUpdateTick, MinimumCpuPollingInterval);
                bool shouldUpdateGpu = ShouldUpdateHardware(now, ref lastGpuUpdateTick, MinimumGpuPollingInterval);
                bool shouldUpdateMemory = ShouldUpdateHardware(now, ref lastMemoryUpdateTick, MinimumMemoryPollingInterval);
                bool shouldUpdateBattery = ShouldUpdateHardware(now, ref lastBatteryUpdateTick, MinimumBatteryPollingInterval);

                foreach (IHardware? hardware in computer.Hardware)
                {
                    if (!ShouldUpdateHardware(hardware, shouldUpdateCpu, shouldUpdateGpu, shouldUpdateMemory, shouldUpdateBattery))
                        continue;

                    try { hardware.Update(); } catch { /* keep going */ }

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

        private int GetPollingInterval(int minimumInterval)
        {
            return Math.Max(updateInterval, minimumInterval);
        }

        private bool ShouldUpdateHardware(long now, ref long lastUpdateTick, int minimumInterval)
        {
            if (lastUpdateTick != 0 && now - lastUpdateTick < GetPollingInterval(minimumInterval))
                return false;

            lastUpdateTick = now;
            return true;
        }

        private static bool ShouldUpdateHardware(IHardware hardware, bool shouldUpdateCpu, bool shouldUpdateGpu, bool shouldUpdateMemory, bool shouldUpdateBattery)
        {
            return hardware.HardwareType switch
            {
                HardwareType.Cpu => shouldUpdateCpu,
                HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => shouldUpdateGpu,
                HardwareType.Memory => shouldUpdateMemory,
                HardwareType.Battery => shouldUpdateBattery,
                _ => false,
            };
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
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "GPU Memory Used")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemory != value)
                {
                    GPUMemory = value;
                    GPUMemoryChanged?.Invoke(GPUMemory);
                }
            }
            else if (sensor.Name == "D3D Dedicated Memory Used")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemoryDedicated != value)
                {
                    GPUMemoryDedicated = value;
                    GPUMemoryDedicatedChanged?.Invoke(GPUMemoryDedicated);
                }
            }
            else if (sensor.Name == "D3D Shared Memory Used")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemoryShared != value)
                {
                    GPUMemoryShared = value;
                    GPUMemorySharedChanged?.Invoke(GPUMemoryShared);
                }
            }
            else if (sensor.Name == "GPU Memory Total")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemoryTotal != value)
                    GPUMemoryTotal = value;
            }
            else if (sensor.Name == "D3D Dedicated Memory Total")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemoryDedicatedTotal != value)
                    GPUMemoryDedicatedTotal = value;
            }
            else if (sensor.Name == "D3D Shared Memory Total")
            {
                float value = sensorValue.Value / 1024.0f; // MB to GB
                if (GPUMemorySharedTotal != value)
                    GPUMemorySharedTotal = value;
            }
        }

        private void HandleGPU_Load(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "D3D 3D")
            {
                float value = sensorValue.Value;
                if (GPULoad != value)
                {
                    GPULoad = value;
                    GPULoadChanged?.Invoke(GPULoad);
                }
            }
        }

        private float HandleGPU_Clock(ISensor sensor, float currentHighest)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return currentHighest;

            if (sensor.Name == "GPU Core")
            {
                float value = sensorValue.Value;
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
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            switch (sensor.Name)
            {
                case "GPU SoC":
                    //case "GPU Package":
                    {
                        float value = sensorValue.Value;
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
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "GPU Core")
            {
                float value = sensorValue.Value;
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
            bool cpuTemperatureMatched = false;

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
                        cpuTemperatureMatched |= HandleCPU_Temperature(sensor);
                        break;
                }
            }

            // Fallback for CPUs whose package temperature LibreHardwareMonitor cannot read
            // (e.g. very recent Intel parts such as the X2's Panther Lake, where every MSR
            // temperature sensor reports null). Let the device supply the temperature so the
            // fan curve and OSD keep working.
            if (!cpuTemperatureMatched)
            {
                float? deviceTemperature = IDevice.GetCurrent().ReadCPUTemperature();
                if (deviceTemperature.HasValue && CPUTemperature != deviceTemperature)
                {
                    CPUTemperature = deviceTemperature;
                    CPUTemperatureChanged?.Invoke(CPUTemperature);
                }
            }
        }

        private void HandleCPU_Load(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "CPU Total")
            {
                float value = sensorValue.Value;
                if (CPULoad != value)
                {
                    CPULoad = value;
                    CPULoadChanged?.Invoke(CPULoad);
                }
            }
        }

        private float HandleCPU_Clock(ISensor sensor, float currentHighest)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return currentHighest;

            if (sensor.Name.StartsWith("CPU Core #", StringComparison.Ordinal) || sensor.Name.StartsWith("Core #", StringComparison.Ordinal))
            {
                float value = sensorValue.Value;
                if (value > currentHighest)
                {
                    if (CPUClock != value)
                    {
                        CPUClock = value;
                        CPUClockChanged?.Invoke(CPUClock);
                    }
                    return value;
                }
            }
            return currentHighest;
        }

        private void HandleCPU_Power(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            switch (sensor.Name)
            {
                case "Package":
                case "CPU Package":
                    {
                        float value = sensorValue.Value;
                        if (CPUPower != value)
                        {
                            CPUPower = value;
                            CPUPowerChanged?.Invoke(CPUPower);
                        }
                    }
                    break;
            }
        }

        private bool HandleCPU_Temperature(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return false;

            if (sensor.Name == "CPU Package" || sensor.Name == "Core (Tctl/Tdie)")
            {
                float value = sensorValue.Value;
                if (CPUTemperature != value)
                {
                    CPUTemperature = value;
                    CPUTemperatureChanged?.Invoke(CPUTemperature);
                }

                return true;
            }

            return false;
        }
        #endregion

        #region memory updates
        public float? GetMemoryUsage() => computer?.IsMemoryEnabled ?? false ? MemoryUsage : null;
        public float? GetMemoryAvailable() => computer?.IsMemoryEnabled ?? false ? MemoryAvailable : null;
        public float? GetMemoryTotal() => GetMemoryUsage() + GetMemoryAvailable();

        private void HandleMemory(IHardware memory)
        {
            // Only read physical RAM; skip VirtualMemory (page file) hardware
            if (memory.Name != "Total Memory")
                return;

            foreach (var sensor in memory.Sensors)
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
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "Memory Used")
            {
                float value = sensorValue.Value;
                if (MemoryUsage != value)
                {
                    MemoryUsage = value;
                    MemoryUsageChanged?.Invoke(MemoryUsage);
                }
            }
            else if (sensor.Name == "Memory Available")
            {
                float value = sensorValue.Value;
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
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "Charge Level")
            {
                float value = sensorValue.Value;
                if (BatteryLevel != value)
                {
                    BatteryLevel = value;
                    BatteryLevelChanged?.Invoke(BatteryLevel);
                }
            }
        }

        private void HandleBattery_Power(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "Charge Rate")
            {
                float value = sensorValue.Value;
                if (BatteryPower != value)
                {
                    BatteryPower = value;
                    BatteryPowerChanged?.Invoke(BatteryPower);
                }
            }
            if (sensor.Name == "Discharge Rate")
            {
                float value = -sensorValue.Value;
                if (BatteryPower != value)
                {
                    BatteryPower = value;
                    BatteryPowerChanged?.Invoke(BatteryPower);
                }
            }
        }

        private void HandleBattery_TimeSpan(ISensor sensor)
        {
            float? sensorValue = sensor.Value;
            if (!sensorValue.HasValue)
                return;

            if (sensor.Name == "Remaining Time (Estimated)")
            {
                float value = sensorValue.Value / 60.0f;
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

        public event ChangedHandler? CPULoadChanged;
        public event ChangedHandler? CPUPowerChanged;
        public event ChangedHandler? CPUClockChanged;
        public event ChangedHandler? CPUTemperatureChanged;

        public event ChangedHandler? GPULoadChanged;
        public event ChangedHandler? GPUPowerChanged;
        public event ChangedHandler? GPUClockChanged;
        public event ChangedHandler? GPUTemperatureChanged;
        public event ChangedHandler? GPUMemoryChanged;
        public event ChangedHandler? GPUMemoryDedicatedChanged;
        public event ChangedHandler? GPUMemorySharedChanged;

        public event ChangedHandler? MemoryUsageChanged;
        public event ChangedHandler? MemoryAvailableChanged;

        public event ChangedHandler? BatteryLevelChanged;
        public event ChangedHandler? BatteryPowerChanged;
        public event ChangedHandler? BatteryTimeSpanChanged;
        #endregion
    }
}