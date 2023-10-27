using HandheldCompanion.Managers;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Platforms
{
    public class OpenHardwareMonitor : IPlatform
    {
        private Computer computer;

        private Timer updateTimer;
        private int updateInterval = 1000;

        public OpenHardwareMonitor()
        {
            Name = "OpenHardwareMonitor";
            IsInstalled = true;

            // watchdog to populate sensors
            updateTimer = new Timer(updateInterval) { Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            // prepare for sensors reading
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
            };

            computer.Open();
        }

        public override bool Start()
        {
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

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            switch (sensor.Name)
                            {
                                case "CPU Package":
                                case "Core (Tctl/Tdie)":
                                    CpuTemperatureChanged?.Invoke((double)sensor.Value);
                                    break;
                            }
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd || hardware.HardwareType == HardwareType.GpuIntel)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            // do something
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
