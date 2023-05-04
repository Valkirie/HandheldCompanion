using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Platforms
{
    public class HWiNFO : IPlatform
    {
        #region struct
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SharedMemory
        {
            public uint dwSignature;
            public uint dwVersion;
            public uint dwRevision;
            public long poll_time;
            public uint dwOffsetOfSensorSection;
            public uint dwSizeOfSensorElement;
            public uint dwNumSensorElements;
            public uint dwOffsetOfReadingSection;
            public uint dwSizeOfReadingElement;
            public uint dwNumReadingElements;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SensorStructure
        {
            public uint dwSensorID;
            public uint dwSensorInst;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string szSensorNameOrig;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string szSensorNameUser;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SensorElement
        {
            public SENSOR_READING_TYPE tReading;
            public uint dwSensorIndex;
            public uint dwSensorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string szLabelOrig;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string szLabelUser;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_UNIT_STRING_LEN)]
            public string szUnit;
            public double Value;
            public double ValueMin;
            public double ValueMax;
            public double ValueAvg;
        }

        public enum SENSOR_READING_TYPE
        {
            SENSOR_TYPE_NONE,
            SENSOR_TYPE_TEMP,
            SENSOR_TYPE_VOLT,
            SENSOR_TYPE_FAN,
            SENSOR_TYPE_CURRENT,
            SENSOR_TYPE_POWER,
            SENSOR_TYPE_CLOCK,
            SENSOR_TYPE_USAGE,
            SENSOR_TYPE_OTHER,
        }

        public class Sensor
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string NameOrig;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
            public string NameUser;
            public Dictionary<uint, SensorElement> Elements;
        }
        #endregion

        #region events
        public event LimitChangedHandler PowerLimitChanged;
        public delegate void LimitChangedHandler(PowerType type, int limit);
        #endregion

        private const string HWiNFO_SHARED_MEM_FILE_NAME = "Global\\HWiNFO_SENS_SM2";
        private const int HWiNFO_SENSORS_STRING_LEN = 128;
        private const int HWiNFO_UNIT_STRING_LEN = 16;

        private long prevPoll_time = -1;

        private MemoryMappedFile MemoryMapped;
        private MemoryMappedViewAccessor MemoryAccessor;

        private Timer MemoryTimer;
        private const int MemoryInterval = 1000;

        private SharedMemory HWiNFOMemory;
        private List<Sensor> HWiNFOSensors;

        public HWiNFO()
        {
            base.PlatformType = PlatformType.HWiNFO;
            base.KeepAlive = true;

            Name = "HWiNFO64";
            ExecutableName = "HWiNFO64.exe";

            // check if platform is installed
            InstallPath = RegistryUtils.GetString(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HWiNFO64_is1", "InstallLocation");
            if (Path.Exists(InstallPath))
            {
                // update paths
                SettingsPath = Path.Combine(InstallPath, "HWiNFO64.ini");
                ExecutablePath = Path.Combine(InstallPath, ExecutableName);

                // check executable
                IsInstalled = File.Exists(ExecutablePath);
            }

            if (!IsInstalled)
            {
                LogManager.LogWarning("HWiNFO is missing. Please get it from: {0}", "https://www.hwinfo.com/files/hwi_742.exe");
                return;
            }

            // our main watchdog to (re)apply requested settings
            base.PlatformWatchdog = new(3000);
            base.PlatformWatchdog.Elapsed += Watchdog_Elapsed;

            // start HWiNFO if not running
            if (IsRunning())
                Stop();
            Start();

            MemoryTimer = new(MemoryInterval);
            MemoryTimer.Elapsed += (sender, e) => PopulateSensors();

            // initialize variables
            HWiNFOMemory = new();
        }

        private void Watchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // check shared memory
                MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);
            }
            catch
            {
                // shared memory is disabled, halt process
                Stop();
                return;
            }

            // we couldn't poll HWiNFO, halt process
            if (HWiNFOMemory.poll_time == prevPoll_time)
            {
                Stop();
                return;
            }

            // update poll time
            prevPoll_time = HWiNFOMemory.poll_time;

            // connect to shared memory
            if (MemoryMapped is null)
                MemoryMapped = MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);

            // get accessor
            if (MemoryAccessor is null)
                MemoryAccessor = MemoryMapped.CreateViewAccessor(0L, Marshal.SizeOf(typeof(SharedMemory)), MemoryMappedFileAccess.Read);
            MemoryAccessor.Read(0L, out HWiNFOMemory);

            // we listed sensors already
            if (HWiNFOSensors is null)
            {
                // (re)set sensors array
                HWiNFOSensors = new();

                // populate sensors array
                GetSensors();
            }

            MemoryTimer.Start();
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            DisposeMemory();

            if (KeepAlive)
                Start();
        }

        public void GetSensors()
        {
            try
            {
                for (uint index = 0; index < HWiNFOMemory.dwNumSensorElements; ++index)
                {
                    using (MemoryMappedViewStream viewStream = MemoryMapped.CreateViewStream(HWiNFOMemory.dwOffsetOfSensorSection + index * HWiNFOMemory.dwSizeOfSensorElement, HWiNFOMemory.dwSizeOfSensorElement, MemoryMappedFileAccess.Read))
                    {
                        byte[] buffer = new byte[(int)HWiNFOMemory.dwSizeOfSensorElement];
                        viewStream.Read(buffer, 0, (int)HWiNFOMemory.dwSizeOfSensorElement);
                        GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        SensorStructure structure = (SensorStructure)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorStructure));
                        gcHandle.Free();
                        Sensor obj = new Sensor
                        {
                            NameOrig = structure.szSensorNameOrig,
                            NameUser = structure.szSensorNameUser,
                            Elements = new()
                        };
                        HWiNFOSensors.Add(obj);
                    }
                }
            }
            catch
            {
                // do something
            }
        }

        private Dictionary<string, SensorElement> MonitoredSensors = new()
        {
            { "PL1", new SensorElement() },
            { "PL2", new SensorElement() },
            { "CPUFrequency", new SensorElement() },
        };

        public void PopulateSensors()
        {
            try
            {
                for (uint index = 0; index < HWiNFOMemory.dwNumReadingElements; ++index)
                {
                    using (MemoryMappedViewStream viewStream = MemoryMapped.CreateViewStream(HWiNFOMemory.dwOffsetOfReadingSection + index * HWiNFOMemory.dwSizeOfReadingElement, HWiNFOMemory.dwSizeOfReadingElement, MemoryMappedFileAccess.Read))
                    {
                        byte[] buffer = new byte[(int)HWiNFOMemory.dwSizeOfReadingElement];
                        viewStream.Read(buffer, 0, (int)HWiNFOMemory.dwSizeOfReadingElement);
                        GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                        SensorElement sensor = (SensorElement)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorElement));
                        gcHandle.Free();

                        HWiNFOSensors[(int)sensor.dwSensorIndex].Elements[sensor.dwSensorID] = sensor;

                        // Level one
                        // FPS

                        // Level two
                        // Battery % and W, GPU and CPU usage and W, RAM usage, FPS

                        // Level three
                        // GPU and CPU usage and temp and W
                        // VRAM usage
                        // RAM usage
                        // Battery % and W
                        // FPS and latency

                        // Level four
                        // Level three + per CPU % and Mhz + Gamescope + FSR

                        switch (sensor.tReading)
                        {
                            case SENSOR_READING_TYPE.SENSOR_TYPE_TEMP:
                                {
                                    switch(sensor.szLabelOrig)
                                    {
                                        case "CPU Package":
                                        case "CPU (Tctl/Tdie)":
                                            MonitoredSensors["CPUTemperature"] = sensor;
                                            break;

                                        case "GPU Temperature":
                                            MonitoredSensors["GPUTemperature"] = sensor;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_POWER:
                                {
                                    switch(sensor.szLabelOrig)
                                    {
                                        case "CPU Package Power":
                                        case "CPU PPT":
                                            MonitoredSensors["CPUPower"] = sensor;
                                            break;

                                        case "PL1 Power Limit":
                                            {
                                                int reading = (int)Math.Ceiling(sensor.Value);
                                                if (reading != MonitoredSensors["PL1"].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                                sensor.Value = reading;
                                                MonitoredSensors["PL1"] = sensor;
                                            }
                                            break;
                                        case "PL2 Power Limit":
                                            {
                                                int reading = (int)Math.Ceiling(sensor.Value);
                                                if (reading != MonitoredSensors["PL2"].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                                sensor.Value = reading;
                                                MonitoredSensors["PL2"] = sensor;
                                            }
                                            break;

                                        case "GPU SoC Power (VDDCR_SOC)":
                                        case "GPU PPT":
                                            MonitoredSensors["GPUPower"] = sensor;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_USAGE:
                                {
                                    switch(sensor.szLabelOrig)
                                    {
                                        case "GPU Utilization":
                                        case "GPU D3D Usage":
                                            MonitoredSensors["GPUUtilization"] = sensor;
                                            break;

                                        case "CPU PPT SLOW Limit":
                                            {
                                                int reading = (int)Math.Floor(MonitoredSensors["CPUPower"].Value / sensor.Value * 100.0d);
                                                if (reading != MonitoredSensors["PL1"].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                                sensor.Value = reading;
                                                MonitoredSensors["PL1"] = sensor;
                                            }
                                            break;
                                        case "CPU PPT FAST Limit":
                                            {
                                                int reading = (int)Math.Floor(MonitoredSensors["CPUPower"].Value / sensor.Value * 100.0d);
                                                if (reading != MonitoredSensors["PL2"].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Fast, reading);

                                                sensor.Value = reading;
                                                MonitoredSensors["PL2"] = sensor;
                                            }
                                            break;

                                        case "Charge Level":
                                            MonitoredSensors["BatteryChargeLevel"] = sensor;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_CLOCK:
                                {
                                    switch(sensor.szLabelOrig)
                                    {
                                        case "GPU Clock":
                                        case "GPU SoC Clock": // keep me ?
                                            MonitoredSensors["GPUFrequency"] = sensor;
                                            break;

                                        case "Core 0 Clock":
                                        case "Core 1 Clock":
                                        case "Core 2 Clock":
                                        case "Core 3 Clock":
                                        case "Core 4 Clock":
                                        case "Core 5 Clock":
                                        case "Core 6 Clock":
                                        case "Core 7 Clock":
                                        case "Core 8 Clock":
                                        case "Core 9 Clock":
                                        case "Core 10 Clock":
                                        case "Core 11 Clock":
                                        case "Core 12 Clock":
                                        case "Core 13 Clock":
                                        case "Core 14 Clock":
                                        case "Core 15 Clock":
                                        case "Core 16 Clock":
                                        case "Core 17 Clock":
                                        case "Core 18 Clock": // improve me (lol)
                                            {
                                                // we'll keep the highest known frequency right now
                                                if (sensor.Value > MonitoredSensors["CPUFrequency"].Value)
                                                    MonitoredSensors["CPUFrequency"] = sensor;
                                            }
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_VOLT:
                                {
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_OTHER:
                                {
                                }
                                break;
                        }

                        // move me !
                        switch(sensor.szLabelOrig)
                        {
                            case "Remaining Capacity":
                                MonitoredSensors["BatteryRemainingCapacity"] = sensor;
                                break;
                            case "Estimated Remaining Time":
                                MonitoredSensors["BatteryRemainingTime"] = sensor;
                                break;

                            case "Physical Memory Used":
                                MonitoredSensors["PhysicalMemoryUsage"] = sensor;
                                break;
                            case "Virtual Memory Committed":
                                MonitoredSensors["VirtualMemoryUsage"] = sensor;
                                break;

                            case "GPU Memory Usage":
                                MonitoredSensors["GPUMemoryUsage"] = sensor;
                                break;
                        }

                        Debug.WriteLine("PL1: {0}", MonitoredSensors["PL1"].Value);
                        Debug.WriteLine("PL2: {0}", MonitoredSensors["PL2"].Value);
                        // Debug.WriteLine("{0}:\t\t{1} {2}\t{3}", sensor.szLabelOrig, sensor.Value, sensor.szUnit, sensor.tReading);
                    }
                }
            }
            catch
            {
                // do something
            }
        }

        public bool SetProperty(string propertyName, object value)
        {
            try
            {
                IniFile settings = new(SettingsPath);
                settings.Write(propertyName, Convert.ToString(value), "Settings");

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Start()
        {
            if (!IsInstalled)
                return false;
            if (IsRunning())
                return false;

            // Shared Memory Support [12-HOUR LIMIT]
            SetProperty("SensorsSM", 1);

            try
            {
                // set lock
                IsStarting = true;

                var process = Process.Start(new ProcessStartInfo()
                {
                    FileName = ExecutablePath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process is not null)
                {
                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;

                    process.WaitForInputIdle();

                    // release lock
                    IsStarting = false;

                    // start watchdog
                    PlatformWatchdog.Enabled = true;
                    PlatformWatchdog.Start();
                }

                return true;
            }
            catch { }

            return false;
        }

        public override bool Stop()
        {
            if (IsStarting)
                return false;
            if (!IsInstalled)
                return false;
            if (!IsRunning())
                return false;

            Process.Kill();

            return true;
        }

        public override void Dispose()
        {
            DisposeMemory();
            base.Dispose();
        }

        private void DisposeMemory()
        {
            if (MemoryMapped is not null)
            {
                MemoryMapped.Dispose();
                MemoryMapped = null;
            }

            if (MemoryAccessor is not null)
            {
                MemoryAccessor.Dispose();
                MemoryAccessor = null;
            }
            
            MemoryTimer.Stop();
            PlatformWatchdog.Stop();

            prevPoll_time = -1;
        }
    }
}