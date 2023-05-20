using ControllerCommon.Managers;
using HandheldCompanion.Controls;
using HandheldCompanion.Platforms;
using PrecisionTiming;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static HandheldCompanion.Platforms.HWiNFO;

namespace HandheldCompanion.Managers
{
    public static class OSDManager
    {
        private static bool IsInitialized;
        private static short OverlayLevel;

        private static PrecisionTimer RefreshTimer;
        private static int RefreshInterval = 100;

        private static ConcurrentDictionary<int, OSD> OnScreenDisplay = new();

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        // C1: GPU
        // C2: CPU
        // C3: RAM
        // C4: VRAM
        // C5: BATT
        // C6: FPS
        private const string Header = "<C0=FFFFFF><C1=458A6E><C2=4C8DB2><C3=AD7B95><C4=A369A6><C5=F19F86><C6=D76D76><A0=-4><A1=5><A2=-2><A3=-3><A4=-4><A5=-5><S0=-50><S1=50>";
        private static List<string> Content;

        static OSDManager()
        {
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            PlatformManager.RTSS.Hooked += RTSS_Hooked;
            PlatformManager.RTSS.Unhooked += RTSS_Unhooked;

            // timer used to monitor foreground application framerate
            RefreshInterval = SettingsManager.GetInt("OnScreenDisplayRefreshRate");

            RefreshTimer = new PrecisionTimer();
            RefreshTimer.SetAutoResetMode(true);
            RefreshTimer.SetResolution(0);
            RefreshTimer.SetPeriod(RefreshInterval);
            RefreshTimer.Tick += UpdateOSD;
        }

        private static void RTSS_Unhooked(int processId)
        {
            try
            {
                // clear previous display
                if (OnScreenDisplay.TryGetValue(processId, out var OSD))
                {
                    OSD.Update("");
                    OSD.Dispose();

                    OnScreenDisplay.TryRemove(new KeyValuePair<int, OSD>(processId, OSD));
                }
            }
            catch { }
        }

        private static void RTSS_Hooked(int processId)
        {
            try
            {
                ProcessEx processEx = ProcessManager.GetProcess(processId);
                OnScreenDisplay[processId] = new(processEx.Title);
            }
            catch { }
        }

        public static void Start()
        {
            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "OSDManager");
        }

        private static uint OSDIndex(this OSD? osd)
        {
            if (osd is null)
                return uint.MaxValue;

            var osdSlot = typeof(OSD).GetField("m_osdSlot",
                   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = osdSlot.GetValue(osd);
            if (value is null)
                return uint.MaxValue;

            return (uint)value;
        }

        private static uint OSDIndex(String name)
        {
            var entries = OSD.GetOSDEntries().ToList();
            for (int i = 0; i < entries.Count(); i++)
            {
                if (entries[i].Owner == name)
                    return (uint)i;
            }
            return 0;
        }

        private static void UpdateOSD(object? sender, EventArgs e)
        {
            if (OverlayLevel == 0)
                return;

            foreach (var pair in OnScreenDisplay)
            {
                int processId = pair.Key;
                OSD processOSD = pair.Value;

                try
                {
                    // recreate OSD if not index 0
                    var idx = OSDIndex(processOSD);
                    if (idx > 110)
                    {
                        processOSD.Dispose();
                        processOSD = null;

                        ProcessEx processEx = ProcessManager.GetProcess(processId);
                        if (processEx is null)
                            continue;
                        processOSD = new(processEx.Title);
                    }

                    string content = Draw(processId);
                    processOSD.Update(content);
                }
                catch { }
            }
        }

        public static string Draw(int processId)
        {
            SensorElement sensor;
            Content = new();

            switch (OverlayLevel)
            {
                default:
                case 0:
                    break;

                case 1:
                    {
                        OverlayRow row1 = new();

                        OverlayEntry FPSentry = new("FPS", "C6");
                        FPSentry.elements.Add(new SensorElement()
                        {
                            Value = PlatformManager.RTSS.GetFramerate(processId),
                            szUnit = "FPS"
                        });
                        row1.entries.Add(FPSentry);

                        // add header to row1
                        Content.Add(Header + row1.ToString());
                    }
                    break;

                case 2:
                    {
                        OverlayRow row1 = new();

                        OverlayEntry BATTentry = new("BATT", "C5");
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.BatteryChargeLevel, out sensor))
                            BATTentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.BatteryRemainingCapacity, out sensor))
                            BATTentry.elements.Add(sensor);
                        row1.entries.Add(BATTentry);

                        OverlayEntry GPUentry = new("GPU", "C1");
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUUsage, out sensor))
                            GPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUPower, out sensor))
                            GPUentry.elements.Add(sensor);
                        row1.entries.Add(GPUentry);

                        OverlayEntry CPUentry = new("CPU", "C2");
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.CPUUsage, out sensor))
                            CPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.CPUPower, out sensor))
                            CPUentry.elements.Add(sensor);
                        row1.entries.Add(CPUentry);

                        OverlayEntry RAMentry = new("RAM", "C3");
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.PhysicalMemoryUsage, out sensor))
                            RAMentry.elements.Add(sensor);
                        row1.entries.Add(RAMentry);

                        OverlayEntry FPSentry = new("FPS", "C6");
                        FPSentry.elements.Add(new SensorElement()
                        {
                            Value = PlatformManager.RTSS.GetFramerate(processId),
                            szUnit = "FPS"
                        });
                        row1.entries.Add(FPSentry);

                        // add header to row1
                        Content.Add(Header + row1.ToString());
                    }
                    break;

                case 3:
                    {
                        OverlayRow row1 = new();
                        OverlayRow row2 = new();
                        OverlayRow row3 = new();
                        OverlayRow row4 = new();
                        OverlayRow row5 = new();
                        OverlayRow row6 = new();

                        OverlayEntry GPUentry = new("GPU", "C1", true);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUUsage, out sensor))
                            GPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUPower, out sensor))
                            GPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUTemperature, out sensor))
                            GPUentry.elements.Add(sensor);
                        row1.entries.Add(GPUentry);

                        OverlayEntry CPUentry = new("CPU", "C2", true);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.CPUUsage, out sensor))
                            CPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.CPUPower, out sensor))
                            CPUentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.CPUTemperature, out sensor))
                            CPUentry.elements.Add(sensor);
                        row2.entries.Add(CPUentry);

                        OverlayEntry RAMentry = new("RAM", "C3", true);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.PhysicalMemoryUsage, out sensor))
                            RAMentry.elements.Add(sensor);
                        row3.entries.Add(RAMentry);

                        OverlayEntry VRAMentry = new("VRAM", "C4", true);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.GPUMemoryUsage, out sensor))
                            VRAMentry.elements.Add(sensor);
                        row4.entries.Add(VRAMentry);

                        OverlayEntry BATTentry = new("BATT", "C5", true);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.BatteryChargeLevel, out sensor))
                            BATTentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.BatteryRemainingCapacity, out sensor))
                            BATTentry.elements.Add(sensor);
                        if (PlatformManager.HWiNFO.MonitoredSensors.TryGetValue(SensorElementType.BatteryRemainingTime, out sensor))
                            BATTentry.elements.Add(sensor);
                        row5.entries.Add(BATTentry);

                        OverlayEntry FPSentry = new("FPS", "C6", true);
                        FPSentry.elements.Add(new SensorElement()
                        {
                            Value = PlatformManager.RTSS.GetFramerate(processId),
                            szUnit = "FPS"
                        });
                        row6.entries.Add(FPSentry);

                        // add header to row1
                        Content.Add(Header + row1.ToString());
                        Content.Add(row2.ToString());
                        Content.Add(row3.ToString());
                        Content.Add(row4.ToString());
                        Content.Add(row5.ToString());
                        Content.Add(row6.ToString());
                    }
                    break;
            }

            return string.Join("\n", Content);
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            RefreshTimer.Stop();

            // unhook all processes
            foreach (int processId in OnScreenDisplay.Keys)
                RTSS_Unhooked(processId);

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "OSDManager");
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "OnScreenDisplayLevel":
                    {
                        OverlayLevel = Convert.ToInt16(value);

                        if (OverlayLevel != 0)
                        {
                            if (!RefreshTimer.IsRunning())
                                RefreshTimer.Start();
                        }
                        else
                        {
                            RefreshTimer.Stop();

                            // clear UI on stop
                            foreach (var pair in OnScreenDisplay)
                            {
                                OSD processOSD = pair.Value;
                                processOSD.Update("");
                            }
                        }
                    }
                    break;

                case "OnScreenDisplayRefreshRate":
                    {
                        RefreshInterval = Convert.ToInt32(value);

                        if (RefreshTimer.IsRunning())
                        {
                            RefreshTimer.Stop();
                            RefreshTimer.SetPeriod(RefreshInterval);
                            RefreshTimer.Start();
                        }
                    }
                    break;
            }
        }
    }

    public class OverlayEntry : IDisposable
    {
        public List<SensorElement> elements = new();
        public string Name { get; set; }

        public OverlayEntry(string name, string colorScheme = "", bool indent = false)
        {
            this.Name = indent ? name + "\t" : name;

            if (!string.IsNullOrEmpty(colorScheme))
                this.Name = "<" + colorScheme + ">" + this.Name + "<C>";
        }

        public void Dispose()
        {
            elements.Clear();
            elements = null;
        }
    }

    public class OverlayRow : IDisposable
    {
        public List<OverlayEntry> entries = new();

        public override string ToString()
        {
            List<string> rowStr = new();

            foreach (OverlayEntry entry in entries)
            {
                if (entry.elements is null || entry.elements.Count == 0)
                    continue;

                List<string> entriesStr = new() { entry.Name };

                foreach (SensorElement element in entry.elements)
                    entriesStr.Add(element.ToString());

                var ItemStr = string.Join(" ", entriesStr);
                rowStr.Add(ItemStr);
            }

            return string.Join(" | ", rowStr);
        }

        public void Dispose()
        {
            entries.Clear();
            entries = null;
        }
    }
}
