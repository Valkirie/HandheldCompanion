using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using static HandheldCompanion.Platforms.HWiNFO;
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

            public override string ToString()
            {
                return string.Format("<C4>{0:00}<S1>{1}<S><C>", Value, szUnit);
            }
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
            public ConcurrentDictionary<uint, SensorElement> Elements;
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
        private byte prevPoll_attempt;

        private MemoryMappedFile MemoryMapped;
        private MemoryMappedViewAccessor MemoryAccessor;

        private Timer MemoryTimer;
        private const int MemoryInterval = 1000;

        private SharedMemory HWiNFOMemory;

        private ConcurrentDictionary<uint, Sensor> HWiNFOSensors = new();
        public ConcurrentDictionary<string, SensorElement> MonitoredSensors = new();

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

            // initialize a few values
            // todo: improve me
            MonitoredSensors["PL1"] = new SensorElement();
            MonitoredSensors["PL2"] = new SensorElement();
            MonitoredSensors["CPUFrequency"] = new SensorElement();

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
                if (prevPoll_time != -1)
                    Stop();
                return;
            }

            // we couldn't poll HWiNFO, halt process
            if (HWiNFOMemory.poll_time == prevPoll_time)
            {
                prevPoll_attempt++;
                if (prevPoll_attempt == 2)
                {
                    Stop();
                    return;
                }
            }

            // update poll time
            if (HWiNFOMemory.poll_time != 0)
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
                HWiNFOSensors.Clear();

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
                        Sensor sensor = new Sensor
                        {
                            NameOrig = structure.szSensorNameOrig,
                            NameUser = structure.szSensorNameUser,
                            Elements = new()
                        };
                        HWiNFOSensors[index] = sensor;
                    }
                }
            }
            catch
            {
                // do something
            }
        }
        
        public SortedDictionary<SensorElementType, SensorElement> MonitoredSensors = new()
        {
            // those are used for computes
            { SensorElementType.PL1, new SensorElement() },
            { SensorElementType.PL2, new SensorElement() },
            { SensorElementType.CPUFrequency, new SensorElement() },
        };

        public enum SensorElementType
        {
            CPUTemperature,
            CPUFrequency,
            CPUPower,
            CPUUsage,

            GPUTemperature,
            GPUFrequency,
            GPUPower,
            GPUUsage,
            GPUMemoryUsage,

            PL1,
            PL2,

            BatteryChargeLevel,
            BatteryRemainingCapacity,
            BatteryRemainingTime,

            PhysicalMemoryUsage,
            VirtualMemoryUsage
        }
        
        public void PopulateSensors()
        {
            if (MemoryMapped is null)
                return;

            try
            {
                for (uint index = 0; index < HWiNFOMemory.dwNumReadingElements; ++index)
                {
                    using (MemoryMappedViewStream viewStream = MemoryMapped.CreateViewStream(HWiNFOMemory.dwOffsetOfReadingSection + index * HWiNFOMemory.dwSizeOfReadingElement, HWiNFOMemory.dwSizeOfReadingElement, MemoryMappedFileAccess.Read))
                    {
                        byte[] buffer = new byte[(int)HWiNFOMemory.dwSizeOfReadingElement];
                        viewStream.Read(buffer, 0, (int)HWiNFOMemory.dwSizeOfReadingElement);
                        GCHandle gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                        SensorElement element = (SensorElement)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorElement));
                        gcHandle.Free();

                        if (HWiNFOSensors.TryGetValue(element.dwSensorIndex, out Sensor sensor))
                            sensor.Elements[element.dwSensorID] = element;
                        else
                            continue;

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

                        switch (element.tReading)
                        {
                            case SENSOR_READING_TYPE.SENSOR_TYPE_TEMP:
                                {
                                    switch(element.szLabelOrig)
                                    {
                                        case "CPU Package":
                                        case "CPU (Tctl/Tdie)":
                                            MonitoredSensors[SensorElementType.CPUTemperature] = element;
                                            break;

                                        case "GPU Temperature":
                                            MonitoredSensors[SensorElementType.GPUTemperature] = element;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_POWER:
                                {
                                    switch(element.szLabelOrig)
                                    {
                                        case "CPU Package Power":
                                        case "CPU PPT":
                                            MonitoredSensors[SensorElementType.CPUPower] = element;
                                            break;

                                        case "PL1 Power Limit":
                                        // case "PL1 Power Limit (Static)":
                                        case "PL1 Power Limit (Dynamic)":
                                            {
                                                int reading = (int)Math.Ceiling(element.Value);
                                                if (reading != MonitoredSensors[SensorElementType.PL1].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                                element.Value = reading;
                                                MonitoredSensors[SensorElementType.PL1] = element;
                                            }
                                            break;
                                        case "PL2 Power Limit":
                                        // case "PL2 Power Limit (Static)":
                                        case "PL2 Power Limit (Dynamic)":
                                            {
                                                int reading = (int)Math.Ceiling(element.Value);
                                                if (reading != MonitoredSensors[SensorElementType.PL2].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Fast, reading);

                                                element.Value = reading;
                                                MonitoredSensors[SensorElementType.PL2] = element;
                                            }
                                            break;

                                        case "GPU SoC Power (VDDCR_SOC)":
                                        case "GPU PPT":
                                            MonitoredSensors[SensorElementType.GPUPower] = element;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_USAGE:
                                {
                                    switch(element.szLabelOrig)
                                    {
                                        case "GPU Utilization":
                                        case "GPU D3D Usage":
                                            MonitoredSensors[SensorElementType.GPUUsage] = element;
                                            break;

                                        case "Total CPU Usage":
                                            MonitoredSensors[SensorElementType.CPUUsage] = element;
                                            break;

                                        case "CPU PPT SLOW Limit":
                                            {
                                                int reading = (int)Math.Floor(MonitoredSensors[SensorElementType.CPUPower].Value / element.Value * 100.0d);
                                                if (reading != MonitoredSensors[SensorElementType.PL1].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                                element.Value = reading;
                                                MonitoredSensors[SensorElementType.PL1] = element;
                                            }
                                            break;
                                        case "CPU PPT FAST Limit":
                                            {
                                                int reading = (int)Math.Floor(MonitoredSensors[SensorElementType.CPUPower].Value / element.Value * 100.0d);
                                                if (reading != MonitoredSensors[SensorElementType.PL2].Value)
                                                    PowerLimitChanged?.Invoke(PowerType.Fast, reading);

                                                element.Value = reading;
                                                MonitoredSensors[SensorElementType.PL2] = element;
                                            }
                                            break;

                                        case "Charge Level":
                                            MonitoredSensors[SensorElementType.BatteryChargeLevel] = element;
                                            break;
                                    }
                                }
                                break;

                            case SENSOR_READING_TYPE.SENSOR_TYPE_CLOCK:
                                {
                                    switch(element.szLabelOrig)
                                    {
                                        case "GPU Clock":
                                        case "GPU SoC Clock": // keep me ?
                                            MonitoredSensors[SensorElementType.GPUFrequency] = element;
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
                                                if (element.Value > MonitoredSensors[SensorElementType.CPUFrequency].Value)
                                                    MonitoredSensors[SensorElementType.CPUFrequency] = element;
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
                        switch(element.szLabelOrig)
                        {
                            case "Remaining Capacity":
                                MonitoredSensors[SensorElementType.BatteryRemainingCapacity] = element;
                                break;
                            case "Estimated Remaining Time":
                                MonitoredSensors[SensorElementType.BatteryRemainingTime] = element;
                                break;

                            case "Physical Memory Used":
                                MonitoredSensors[SensorElementType.PhysicalMemoryUsage] = element;
                                break;
                            case "Virtual Memory Committed":
                                MonitoredSensors[SensorElementType.VirtualMemoryUsage] = element;
                                break;

                            case "GPU Memory Usage":
                                MonitoredSensors[SensorElementType.GPUMemoryUsage] = element;
                                break;
                        }

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

            // Quiet startup
            // Todo: make this configurable ?
            SetProperty("OpenSensors", 1);
            SetProperty("MinimalizeSensors", 1);

            try
            {
                // set lock
                IsStarting = true;

                var process = Process.Start(new ProcessStartInfo()
                {
                    FileName = ExecutablePath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    CreateNoWindow = true
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

            Kill();

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
            prevPoll_attempt = 0;
        }
    }
}