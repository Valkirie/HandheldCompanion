using LibreHardwareMonitor.Hardware;
using System.Timers;

namespace HandheldCompanion.Platforms
{
    public class OpenHardwareMonitor : IPlatform
    {
        private Computer computer;
        private string ProductName;

        private Timer updateTimer;
        private int updateInterval = 1000;

        public OpenHardwareMonitor()
        {
            Name = "OpenHardwareMonitor";
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
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();

                    foreach (ISensor? sensor in hardware.Sensors)
                    {
                        if (sensor.Value is null)
                            continue;

                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            switch (sensor.Name)
                            {
                                case "CPU Package":
                                case "Core (Tctl/Tdie)":
                                    {
                                        double value = (double)sensor.Value;

                                        // dirty
                                        switch(ProductName)
                                        {
                                            case "Galileo":
                                                value /= 2.0d;
                                                break;
                                        }

                                        CpuTemperatureChanged?.Invoke(value);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        #region events

        public event CpuTemperatureChangedHandler CpuTemperatureChanged;
        public delegate void CpuTemperatureChangedHandler(double value);

        #endregion
    }
}
