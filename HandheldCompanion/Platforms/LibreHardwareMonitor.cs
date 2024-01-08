using LibreHardwareMonitor.Hardware;
using System.Timers;

namespace HandheldCompanion.Platforms
{
    public class LibreHardwareMonitor : IPlatform
    {
        private Computer computer;
        private string ProductName;

        private Timer updateTimer;
        private int updateInterval = 1000;

        public float? CPULoad;
        public float? CPUClock;
        public float? CPUPower;
        public float? CPUTemperatur;

        public float? GPULoad;
        public float? GPUClock;
        public float? GPUPower;
        public float? GPUTemperatur;

        public float? MemoryLoad;

        public float? VRAMLoad;

        public float? BatteryLevel;
        public float? BatteryPower;
        public float? BatteryTimeSpan;

        public LibreHardwareMonitor()
        {
            Name = "LibreHardwareMonitor";
            IsInstalled = true;

            ProductName = MotherboardInfo.Product;

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

        public override bool Start()
        {
            // open computer, slow
            computer.Open();

            updateTimer.Start();

            return base.Start();
        }

        public override bool Stop(bool kill = false)
        {
            if (updateTimer is not null)
                updateTimer.Stop();

            if (computer is not null)
                computer.Close();

            return base.Stop(kill);
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // pull temperature sensor
            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();
                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        HandleCPU(hardware);
                        break;
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                    case HardwareType.GpuNvidia:
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

        #region cpu updates
        private void HandleCPU(IHardware cpu)
        {
            float highestClock = 0;
            foreach (var sensor in cpu.Sensors)
            {
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
                CPUPowerChanged?.Invoke(CPULoad);
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
                    CPULoadChanged?.Invoke(CPUPower);
                    return value;
                }
            }
            return currentHighest;
        }

        private void HandleCPU_Power(ISensor sensor)
        {
            if (sensor.Name == "Package")
            {
                CPUPower = (float)sensor.Value;
                CPULoadChanged?.Invoke(CPUPower);
            }
        }

        private void HandleCPU_Temperatur(ISensor sensor)
        {
            if (sensor.Name == "CPU Package" || sensor.Name == "Core (Tctl/Tdie)")
            {
                CPUTemperatur = (float)sensor.Value;

                // dirty
                switch (ProductName)
                {
                    case "Galileo":
                        CPUTemperatur /= 2.0f;
                        break;
                }

                CPUTemperatureChanged?.Invoke(CPUTemperatur);
            }
        }
        #endregion

        #region gpu updates
        private void HandleGPU(IHardware cpu)
        {
            float highestClock = 0;
            foreach (var sensor in cpu.Sensors)
            {
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
            if (sensor.Name == "GPU Core")
            {
                GPULoad = (float)sensor.Value;
                GPULoadChanged?.Invoke(GPULoad);
            }
            if (sensor.Name == "GPU Memory")
            {
                VRAMLoad = (float)sensor.Value;
                VRAMLoadChanged?.Invoke(VRAMLoad);
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
            if (sensor.Name == "GPU Core")
            {
                GPUPower = (float)sensor.Value;
                GPUPowerChanged?.Invoke(GPUPower);
            }
        }

        private void HandleGPU_Temperatur(ISensor sensor)
        {
            if (sensor.Name == "GPU Core")
            {
                GPUTemperatur = (float)sensor.Value;
                GPUTemperatureChanged?.Invoke(GPUTemperatur);
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
                    case SensorType.Load:
                        HandleMemory_Load(sensor);
                        break;
                }
            }
        }

        private void HandleMemory_Load(ISensor sensor)
        {
            if (sensor.Name == "Memory")
            {
                MemoryLoad = (float)sensor.Value;
                MemoryLoadChanged?.Invoke(MemoryLoad);
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
        public event ChangedHandler GPUClockChanged;
        public event ChangedHandler GPUPowerChanged;
        public event ChangedHandler GPUTemperatureChanged;

        public event ChangedHandler MemoryLoadChanged;

        public event ChangedHandler VRAMLoadChanged;

        public event ChangedHandler BatteryLevelChanged;
        public event ChangedHandler BatteryPowerChanged;
        public event ChangedHandler BatteryTimeSpanChanged;
        #endregion
    }
}