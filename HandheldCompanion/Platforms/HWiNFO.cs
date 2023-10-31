using HandheldCompanion.Managers;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Platforms;

public class HWiNFO : IPlatform
{
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

    private const string HWiNFO_SHARED_MEM_FILE_NAME = "Global\\HWiNFO_SENS_SM2";
    private const int HWiNFO_SENSORS_STRING_LEN = 128;
    private const int HWiNFO_UNIT_STRING_LEN = 16;
    private const int MemoryInterval = 1000;

    private readonly Timer MemoryTimer;

    private SharedMemory HWiNFOMemory;

    private ConcurrentDictionary<uint, Sensor> HWiNFOSensors;
    private MemoryMappedViewAccessor MemoryAccessor;

    private MemoryMappedFile MemoryMapped;

    public ConcurrentDictionary<SensorElementType, SensorElement> MonitoredSensors = new();

    private long prevPoll_time = -1;

    public HWiNFO()
    {
        PlatformType = PlatformType.HWiNFO;
        ExpectedVersion = new Version(7, 42, 5030);
        Url = "https://www.hwinfo.com/files/hwi_742.exe";

        Name = "HWiNFO64";
        ExecutableName = RunningName = "HWiNFO64.exe";

        // check if platform is installed
        InstallPath = RegistryUtils.GetString(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HWiNFO64_is1",
            "InstallLocation");
        if (Path.Exists(InstallPath))
        {
            // update paths
            SettingsPath = Path.Combine(InstallPath, "HWiNFO64.ini");
            ExecutablePath = Path.Combine(InstallPath, ExecutableName);

            // check executable
            if (File.Exists(ExecutablePath))
            {
                // check executable version
                var versionInfo = FileVersionInfo.GetVersionInfo(ExecutablePath);
                var CurrentVersion = new Version(versionInfo.ProductMajorPart, versionInfo.ProductMinorPart,
                    versionInfo.ProductBuildPart);

                if (CurrentVersion < ExpectedVersion)
                {
                    LogManager.LogWarning("HWiNFO is outdated. Please get it from: {0}", Url);
                    return;
                }

                IsInstalled = true;
            }
        }

        if (!IsInstalled)
        {
            LogManager.LogWarning("HWiNFO is missing. Please get it from: {0}", Url);
            return;
        }

        // those are used for computes
        MonitoredSensors[SensorElementType.PL1] = new SensorElement();
        MonitoredSensors[SensorElementType.PL2] = new SensorElement();
        MonitoredSensors[SensorElementType.CPUFrequency] = new SensorElement();
        MonitoredSensors[SensorElementType.GPUFrequency] = new SensorElement();

        // our main watchdog to (re)apply requested settings
        PlatformWatchdog = new Timer(3000) { Enabled = false };
        PlatformWatchdog.Elapsed += Watchdog_Elapsed;

        // secondary watchdog to (re)populate sensors
        MemoryTimer = new Timer(MemoryInterval) { Enabled = false };
        MemoryTimer.Elapsed += (sender, e) => PopulateSensors();
    }

    public override bool Start()
    {
        // start HWiNFO if not running or Shared Memory is disabled
        var hasSensorsSM = GetProperty("SensorsSM");
        if (!IsRunning || !hasSensorsSM)
        {
            StopProcess();
            StartProcess();
        }
        else
        {
            // hook into current process
            Process.Exited += Process_Exited;
        }

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        return base.Start();
    }

    public override bool Stop(bool kill = false)
    {
        if (MemoryTimer is not null)
            MemoryTimer.Stop();

        SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        return base.Stop(kill);
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "OnScreenDisplayRefreshRate":
                    SetProperty("SensorInterval", Convert.ToInt32(value));
                    break;
            }
        });
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
                StopProcess();

            // HWiNFO is loading
            return;
        }

        // we couldn't poll HWiNFO, halt process
        if (HWiNFOMemory.poll_time == prevPoll_time)
        {
            StopProcess();
            return;
        }

        // update poll time
        if (HWiNFOMemory.poll_time != 0)
            prevPoll_time = HWiNFOMemory.poll_time;

        // reset tentative counter
        Tentative = 0;

        // connect to shared memory
        if (MemoryMapped is null)
            MemoryMapped = MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);

        // get accessor
        if (MemoryAccessor is null)
            MemoryAccessor =
                MemoryMapped.CreateViewAccessor(0L, Marshal.SizeOf(typeof(SharedMemory)), MemoryMappedFileAccess.Read);
        MemoryAccessor.Read(0L, out HWiNFOMemory);

        // we listed sensors already
        if (HWiNFOSensors is null)
        {
            // (re)set sensors array
            HWiNFOSensors = new ConcurrentDictionary<uint, Sensor>();

            // populate sensors array
            GetSensors();
        }

        MemoryTimer.Start();
    }

    public void GetSensors()
    {
        try
        {
            for (uint index = 0; index < HWiNFOMemory.dwNumSensorElements; ++index)
                using (var viewStream = MemoryMapped.CreateViewStream(
                           HWiNFOMemory.dwOffsetOfSensorSection + index * HWiNFOMemory.dwSizeOfSensorElement,
                           HWiNFOMemory.dwSizeOfSensorElement, MemoryMappedFileAccess.Read))
                {
                    var buffer = new byte[(int)HWiNFOMemory.dwSizeOfSensorElement];
                    viewStream.Read(buffer, 0, (int)HWiNFOMemory.dwSizeOfSensorElement);
                    var gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    var structure =
                        (SensorStructure)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorStructure));
                    gcHandle.Free();
                    var sensor = new Sensor
                    {
                        NameOrig = structure.szSensorNameOrig,
                        NameUser = structure.szSensorNameUser,
                        Elements = new ConcurrentDictionary<uint, SensorElement>()
                    };
                    HWiNFOSensors[index] = sensor;
                }
        }
        catch
        {
            // do something
        }
    }

    public void PopulateSensors()
    {
        if (MemoryMapped is null)
            return;

        try
        {
            for (uint index = 0; index < HWiNFOMemory.dwNumReadingElements; ++index)
                using (var viewStream = MemoryMapped.CreateViewStream(
                           HWiNFOMemory.dwOffsetOfReadingSection + index * HWiNFOMemory.dwSizeOfReadingElement,
                           HWiNFOMemory.dwSizeOfReadingElement, MemoryMappedFileAccess.Read))
                {
                    var buffer = new byte[(int)HWiNFOMemory.dwSizeOfReadingElement];
                    viewStream.Read(buffer, 0, (int)HWiNFOMemory.dwSizeOfReadingElement);
                    var gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                    var element =
                        (SensorElement)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorElement));
                    gcHandle.Free();

                    if (HWiNFOSensors.TryGetValue(element.dwSensorIndex, out var sensor))
                        sensor.Elements[element.dwSensorID] = element;
                    else
                        continue;

                    switch (element.tReading)
                    {
                        case SENSOR_READING_TYPE.SENSOR_TYPE_TEMP:
                            {
                                switch (element.szLabelOrig)
                                {
                                    case "CPU Package":
                                    case "CPU (Tctl/Tdie)":
                                        MonitoredSensors[SensorElementType.CPUTemperature] = element;
                                        break;

                                    case "CPU GT Cores (Graphics)":
                                    case "GPU Temperature":
                                        MonitoredSensors[SensorElementType.GPUTemperature] = element;
                                        break;
                                }
                            }
                            break;

                        case SENSOR_READING_TYPE.SENSOR_TYPE_POWER:
                            {
                                switch (element.szLabelOrig)
                                {
                                    case "CPU Package Power":
                                    case "CPU PPT":
                                        MonitoredSensors[SensorElementType.CPUPower] = element;
                                        break;

                                    case "PL1 Power Limit":
                                    // case "PL1 Power Limit (Static)":
                                    case "PL1 Power Limit (Dynamic)":
                                        {
                                            var reading = (int)Math.Ceiling(element.Value);
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
                                            var reading = (int)Math.Ceiling(element.Value);
                                            if (reading != MonitoredSensors[SensorElementType.PL2].Value)
                                                PowerLimitChanged?.Invoke(PowerType.Fast, reading);

                                            element.Value = reading;
                                            MonitoredSensors[SensorElementType.PL2] = element;
                                        }
                                        break;

                                    case "GPU ASIC Power":
                                    case "GT Cores Power":
                                    case "GPU SoC Power (VDDCR_SOC)":
                                    case "GPU PPT":
                                        MonitoredSensors[SensorElementType.GPUPower] = element;
                                        break;
                                }
                            }
                            break;

                        case SENSOR_READING_TYPE.SENSOR_TYPE_USAGE:
                            {
                                switch (element.szLabelOrig)
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
                                            var reading = (int)Math.Floor(MonitoredSensors[SensorElementType.CPUPower].Value /
                                                element.Value * 100.0d);
                                            if (reading != MonitoredSensors[SensorElementType.PL1].Value)
                                                PowerLimitChanged?.Invoke(PowerType.Slow, reading);

                                            element.Value = reading;
                                            MonitoredSensors[SensorElementType.PL1] = element;
                                        }
                                        break;
                                    case "CPU PPT FAST Limit":
                                        {
                                            var reading = (int)Math.Floor(MonitoredSensors[SensorElementType.CPUPower].Value /
                                                element.Value * 100.0d);
                                            if (reading != MonitoredSensors[SensorElementType.PL2].Value)
                                                PowerLimitChanged?.Invoke(PowerType.Fast, reading);

                                            element.Value = reading;
                                            MonitoredSensors[SensorElementType.PL2] = element;
                                        }
                                        break;
                                }
                            }
                            break;

                        case SENSOR_READING_TYPE.SENSOR_TYPE_CLOCK:
                            {
                                switch (element.szLabelOrig)
                                {
                                    case "GPU Clock":
                                    case "GPU SoC Clock": // keep me ?
                                        {
                                            var reading = element.Value;
                                            if (reading != MonitoredSensors[SensorElementType.GPUFrequency].Value)
                                                GPUFrequencyChanged?.Invoke(reading);

                                            MonitoredSensors[SensorElementType.GPUFrequency] = element;
                                        }
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
                    switch (element.szLabelOrig)
                    {
                        case "Remaining Capacity":
                            MonitoredSensors[SensorElementType.BatteryRemainingCapacity] = element;
                            break;
                        case "Charge Level":
                            MonitoredSensors[SensorElementType.BatteryChargeLevel] = element;
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

                        case "GPU D3D Memory Dynamic":
                        case "GPU Memory Usage":
                            MonitoredSensors[SensorElementType.GPUMemoryUsage] = element;
                            break;
                    }
                    // Debug.WriteLine("{0}:\t\t{1} {2}\t{3}", sensor.szLabelOrig, sensor.Value, sensor.szUnit, sensor.tReading);
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

    public bool GetProperty(string propertyName)
    {
        try
        {
            IniFile settings = new(SettingsPath);
            return Convert.ToBoolean(Convert.ToInt16(settings.Read(propertyName, "Settings")));
        }
        catch
        {
            return false;
        }
    }

    public override bool StartProcess()
    {
        if (!IsInstalled)
            return false;

        if (IsRunning)
            KillProcess();

        // (re)set elements
        DisposeMemory();

        // Quiet startup
        SetProperty("OpenSystemSummary", 0);
        SetProperty("OpenSensors", 1);
        SetProperty("MinimalizeMainWnd", 1);
        SetProperty("MinimalizeSensors", 1);
        SetProperty("MinimalizeSensorsClose", 1);
        SetProperty("SensorsSM", 1); // Shared Memory Support [12-HOUR LIMIT]
        SetProperty("ShowWelcomeAndProgress", 0);
        SetProperty("SensorsOnly", 1);
        SetProperty("AutoUpdateBetaDisable", 1);
        SetProperty("AutoUpdate", 0);

        // stop watchdog
        PlatformWatchdog.Stop();

        return base.StartProcess();
    }

    public override bool StopProcess()
    {
        if (IsStarting)
            return false;

        KillProcess();

        return true;
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

        if (HWiNFOSensors is not null)
            HWiNFOSensors = null;

        prevPoll_time = -1;
    }

    public override void Dispose()
    {
        DisposeMemory();
        base.Dispose();
    }

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
        public double FanValueMin;
        public double FanValueMax;
        public double ValueAvg;

        public override string ToString()
        {
            return string.Format("<C0>{0:00}<S1>{1}<S><C>", Value, szUnit);
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
        SENSOR_TYPE_OTHER
    }

    public class Sensor
    {
        public ConcurrentDictionary<uint, SensorElement> Elements;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
        public string NameOrig;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_SENSORS_STRING_LEN)]
        public string NameUser;
    }

    #endregion

    #region events

    public event LimitChangedHandler PowerLimitChanged;

    public delegate void LimitChangedHandler(PowerType type, int limit);

    public event GPUFrequencyChangedHandler GPUFrequencyChanged;

    public delegate void GPUFrequencyChangedHandler(double value);

    #endregion
}