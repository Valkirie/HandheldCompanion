using HandheldCompanion.Managers;
using LibreHardwareMonitor.Hardware;
using System;
using System.Timers;

namespace HandheldCompanion.Platforms
{
    public class LibreHardwareMonitor : IPlatform
    {
        private Computer computer;

        private Timer updateTimer;
        private int updateInterval = 1000;
        private object updateLock = new();

        public float? CPULoad;
        public float? CPUClock;
        public float? CPUPower;
        public float? CPUTemperature;

        public float? GPULoad;
        public float? GPUClock;
        public float? GPUPower;
        public float? GPUTemperature;

        public float? MemoryUsage;

        public float? BatteryLevel;
        public float? BatteryPower;
        public float? BatteryTimeSpan;

        public LibreHardwareMonitor()
        {
            Name = "LibreHardwareMonitor";
            IsInstalled = true;

            // watchdog to populate sensors
            updateTimer = new Timer(updateInterval) { Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            // prepare for sensors reading
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsBatteryEnabled = true,
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
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

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

            // open computer, slow
            computer?.Open();

            updateTimer?.Start();

            return base.Start();
        }

        private void QuerySettings()
        {
            SettingsManager_SettingValueChanged("OnScreenDisplayRefreshRate", ManagerFactory.settingsManager.GetString("OnScreenDisplayRefreshRate"), false);
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
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
                    hardware.Update();

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
                        HandleGPU_Temperatur(sensor);
                        break;
                }
            }
        }

        private void HandleGPU_Load(ISensor sensor)
        {
            if (sensor.Name == "D3D 3D")
            {
                GPULoad = (float)sensor.Value;
                GPULoadChanged?.Invoke(GPULoad);
            }
        }

        private float HandleGPU_Clock(ISensor sensor, float currentHighest)
        {
            if (sensor.Name == "GPU Core")
            {
                var value = (float)sensor.Value;
                if (value > currentHighest)
                {
                    GPUClock = (float)sensor.Value;
                    GPUClockChanged?.Invoke(GPUClock);
                    return value;
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
                    GPUPower = (float)sensor.Value;
                    GPUPowerChanged?.Invoke(GPUPower);
                    break;
            }
        }

        private void HandleGPU_Temperatur(ISensor sensor)
        {
            if (sensor.Name == "GPU Core")
            {
                GPUTemperature = (float)sensor.Value;
                GPUTemperatureChanged?.Invoke(GPUTemperature);
            }
        }
        #endregion

        #region cpu updates
        private void HandleCPU(IHardware cpu)
        {
            float highestClock = 0;
            foreach (var sensor in cpu.Sensors)
            {
                // May crash the app when Value is null, better to check first
                if (sensor.Value is null)
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
                        HandleCPU_Temperatur(sensor);
                        break;
                }
            }
        }

        private void HandleCPU_Load(ISensor sensor)
        {
            if (sensor.Name == "CPU Total")
            {
                CPULoad = (float)sensor.Value;
                CPULoadChanged?.Invoke(CPULoad);
            }
        }

        private float HandleCPU_Clock(ISensor sensor, float currentHighest)
        {
            if ((sensor.Name.StartsWith("CPU Core #") || sensor.Name.StartsWith("Core #")))
            {
                var value = (float)sensor.Value;
                if (value > currentHighest)
                {
                    CPUClock = (float)sensor.Value;
                    CPUClockChanged?.Invoke(CPUClock);
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
                    CPUPower = (float)sensor.Value;
                    CPUPowerChanged?.Invoke(CPUPower);
                    break;
            }
        }

        private void HandleCPU_Temperatur(ISensor sensor)
        {
            if (sensor.Name == "CPU Package" || sensor.Name == "Core (Tctl/Tdie)")
            {
                CPUTemperature = (float)sensor.Value;
                CPUTemperatureChanged?.Invoke(CPUTemperature);
            }
        }
        #endregion

        #region memory updates
        private void HandleMemory(IHardware cpu)
        {
            foreach (var sensor in cpu.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Data:
                        HandleMemory_Data(sensor);
                        break;
                }
            }
        }

        private void HandleMemory_Data(ISensor sensor)
        {
            if (sensor.Name == "Memory Used")
            {
                MemoryUsage = ((float)sensor.Value) * 1024;
                MemoryUsageChanged?.Invoke(MemoryUsage);
            }
        }
        #endregion

        #region battery updates
        private void HandleBattery(IHardware cpu)
        {
            foreach (var sensor in cpu.Sensors)
            {
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
                BatteryLevel = (float)sensor.Value;
                BatteryLevelChanged?.Invoke(BatteryLevel);
            }
        }

        private void HandleBattery_Power(ISensor sensor)
        {
            if (sensor.Name == "Charge Rate")
            {
                BatteryPower = (float)sensor.Value;
                BatteryPowerChanged?.Invoke(BatteryPower);
            }
            if (sensor.Name == "Discharge Rate")
            {
                BatteryPower = -(float)sensor.Value;
                BatteryPowerChanged?.Invoke(BatteryPower);
            }
        }

        private void HandleBattery_TimeSpan(ISensor sensor)
        {
            if (sensor.Name == "Remaining Time (Estimated)")
            {
                BatteryTimeSpan = ((float)sensor.Value) / 60;
                BatteryTimeSpanChanged?.Invoke(BatteryTimeSpan);
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

        public event ChangedHandler MemoryUsageChanged;

        public event ChangedHandler BatteryLevelChanged;
        public event ChangedHandler BatteryPowerChanged;
        public event ChangedHandler BatteryTimeSpanChanged;
        #endregion
    }
}