using ControllerCommon.Managers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Timers;

namespace HandheldCompanion.Managers
{
    public static class HWiNFOManager
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
        public static event FailedEventHandler HasFailed;
        public delegate void FailedEventHandler();

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private const string HWiNFO_SHARED_MEM_FILE_NAME = "Global\\HWiNFO_SENS_SM2";
        private const int HWiNFO_SENSORS_STRING_LEN = 128;
        private const int HWiNFO_UNIT_STRING_LEN = 16;

        private const short INTERVAL_UPDATE = 1000;                 // interval between two SharedMemory update
        private const short INTERVAL_SHAREDMEMORY = 3000;           // interval between two SharedMemory access check

        private static Timer UpdateTimer;
        private static Timer SharedMemoryTimer;

        private static MemoryMappedFile MemoryMapped;
        private static MemoryMappedViewAccessor MemoryAccessor;

        private static SharedMemory HWiNFOMemory;
        private static List<Sensor> Sensors;

        private static object updateLock = new();
        private static bool IsInitialized;

        static HWiNFOManager()
        {
            HWiNFOMemory = new SharedMemory();

            UpdateTimer = new Timer(INTERVAL_UPDATE);
            UpdateTimer.AutoReset = true;
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

            SharedMemoryTimer = new Timer(INTERVAL_SHAREDMEMORY);
            SharedMemoryTimer.AutoReset = true;
            SharedMemoryTimer.Elapsed += (e, sender) => SharedMemoryTicked();
        }

        private static void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (MemoryMapped is not null)
                ReadSensors();
        }

        private static void SharedMemoryTicked()
        {
            // check if shared memory is enabled
            try
            {
                // connect to shared memory
                MemoryMapped = MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);
                MemoryAccessor = MemoryMapped.CreateViewAccessor(0L, Marshal.SizeOf(typeof(SharedMemory)), MemoryMappedFileAccess.Read);
                MemoryAccessor.Read(0L, out HWiNFOMemory);

                // we're already connected
                Debug.WriteLine("poll_time:{0}", HWiNFOMemory.poll_time);
                if (HWiNFOMemory.poll_time == prevPoll_time)
                {
                    Failed();
                    return;
                }

                // (re)initiliaze sensors library
                Sensors = new();

                // populate sensors names
                ReadSensorNames();
            }
            catch
            {
                Failed();
                return;
            }
        }

        public static void Start()
        {
            // start HWiNFO watcher
            SharedMemoryTimer.Start();

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "HWiNFOManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            // stop HWiNFO watcher
            SharedMemoryTimer.Stop();
            UpdateTimer.Stop();

            // dispose objects
            MemoryMapped.Dispose();
            MemoryAccessor.Dispose();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "HWiNFOManager");
        }

        private static void Failed()
        {
            // HWiNFO is not running anymore or 12-HOUR LIMIT has triggered
            UpdateTimer.Stop();
            HasFailed?.Invoke();
            MemoryMapped = null;
        }

        public static void ReadSensorNames()
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
                    Sensors.Add(obj);
                }
            }

            UpdateTimer.Start();
        }

        private static long prevPoll_time;

        public static void ReadSensors()
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

                        Sensors[(int)sensor.dwSensorIndex].Elements[sensor.dwSensorID] = sensor;

                        if (sensor.tReading == SENSOR_READING_TYPE.SENSOR_TYPE_POWER)
                        {
                            switch(sensor.szLabelOrig)
                            {
                                case "PL1 Power Limit":
                                case "CPU Package Power":
                                case "PL2 Power Limit":
                                    Debug.WriteLine("{0}:{1}", sensor.szLabelOrig, sensor.Value);
                                    break;
                            }

                            continue;
                        }
                    }
                }
            }
            catch
            {
                Failed();
            }
        }
    }
}
