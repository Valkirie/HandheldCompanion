using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

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

        private const string HWiNFO_SHARED_MEM_FILE_NAME = "Global\\HWiNFO_SENS_SM2";
        private const int HWiNFO_SENSORS_STRING_LEN = 128;
        private const int HWiNFO_UNIT_STRING_LEN = 16;

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

            // start HWiNFO if not running
            if (!IsRunning())
                Start();

            // hook into HWiNFO process
            Process.Exited += Process_Exited;

            // our main watchdog to (re)apply requested settings
            base.PlatformWatchdog = new(2000) { Enabled = true };
            base.PlatformWatchdog.Elapsed += Watchdog_Elapsed;
        }

        private void Watchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // do something
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            if (KeepAlive)
                Start();
        }

        public override bool Start()
        {
            if (!IsInstalled)
                return false;

            if (IsRunning())
                return false;

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = ExecutablePath,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            process.EnableRaisingEvents = true;
            process.Exited += Process_Exited;

            process.WaitForInputIdle();

            return process is not null;
        }

        public override bool Stop()
        {
            if (!IsInstalled)
                return false;

            if (!IsRunning())
                return false;

            Process.Kill();

            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}