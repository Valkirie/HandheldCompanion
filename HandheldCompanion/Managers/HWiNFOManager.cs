using ControllerCommon.Managers;
using PrecisionTiming;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

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
            public List<SensorElement> Elements;
        }
        #endregion

        #region events
        public static event FailedEventHandler Failed;
        public delegate void FailedEventHandler();

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private const string HWiNFO_SHARED_MEM_FILE_NAME = "Global\\HWiNFO_SENS_SM2";
        private const int HWiNFO_SENSORS_STRING_LEN = 128;
        private const int HWiNFO_UNIT_STRING_LEN = 16;

        private const short INTERVAL_UPDATE = 1000;                 // interval between two SharedMemory update
        private const short INTERVAL_SHAREDMEMORY = 3000;           // interval between two SharedMemory access check

        private static PrecisionTimer UpdateTimer;
        private static PrecisionTimer SharedMemoryTimer;

        private static MemoryMappedFile MemoryMapped;
        private static MemoryMappedViewAccessor MemoryAccessor;

        private static SharedMemory HWiNFOMemory;
        private static List<Sensor> Sensors;

        private static object updateLock = new();
        private static bool IsInitialized;

        static HWiNFOManager()
        {
            HWiNFOMemory = new SharedMemory();

            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(INTERVAL_UPDATE);
            UpdateTimer.SetAutoResetMode(true);

            SharedMemoryTimer = new PrecisionTimer();
            SharedMemoryTimer.SetInterval(INTERVAL_SHAREDMEMORY);
            SharedMemoryTimer.SetAutoResetMode(true);
            SharedMemoryTimer.Tick += (e, sender) => SharedMemoryTicked();
        }

        private static void SharedMemoryTicked()
        {
            // check if shared memory is enabled
            try
            {
                MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);
            }
            catch
            {
                // HWiNFO is not running anymore or 12-HOUR LIMIT has triggered
                Failed?.Invoke();
                return;
            }

            // we're already connected
            if (MemoryMapped is not null)
                return;

            // connect to shared memory
            MemoryMapped = MemoryMappedFile.OpenExisting(HWiNFO_SHARED_MEM_FILE_NAME, MemoryMappedFileRights.Read);
            MemoryAccessor = MemoryMapped.CreateViewAccessor(0L, Marshal.SizeOf(typeof(SharedMemory)), MemoryMappedFileAccess.Read);
            MemoryAccessor.Read(0L, out HWiNFOMemory);

            // (re)initiliaze sensors library
            Sensors = new();
            // populate sensors names
            ReadSensorNames();
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

            // dispose objects
            MemoryMapped.Dispose();
            MemoryAccessor.Dispose();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "HWiNFOManager");
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
                        Elements = new List<SensorElement>()
                    };
                    Sensors.Add(obj);
                }
            }
        }

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
                        SensorElement structure = (SensorElement)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(SensorElement));
                        gcHandle.Free();
                        Sensors[(int)structure.dwSensorIndex].Elements.Add(structure);
                    }
                }
            }
            catch
            {
                // HWiNFO is not running anymore or 12-HOUR LIMIT has triggered
                MemoryMapped = null;
                Failed?.Invoke();
            }
        }
    }
}
