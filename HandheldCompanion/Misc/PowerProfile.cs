using HandheldCompanion.Managers;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace HandheldCompanion.Misc
{
    [Serializable]
    public class PowerProfile
    {
        public string Name;
        public string Description;

        public string FileName { get; set; }
        public bool Default { get; set; }
        public bool DeviceDefault { get; set; }

        public Version Version { get; set; } = new();
        public Guid Guid { get; set; } = Guid.NewGuid();

        public bool TDPOverrideEnabled { get; set; }
        public double[] TDPOverrideValues { get; set; }

        public bool CPUOverrideEnabled { get; set; }
        public double CPUOverrideValue { get; set; }

        public bool GPUOverrideEnabled { get; set; }
        public double GPUOverrideValue { get; set; }

        public bool AutoTDPEnabled { get; set; }
        public float AutoTDPRequestedFPS { get; set; } = 30.0f;

        public bool EPPOverrideEnabled { get; set; }
        public uint EPPOverrideValue { get; set; } = 50;

        public bool CPUCoreEnabled { get; set; }
        public int CPUCoreCount { get; set; } = MotherboardInfo.NumberOfCores;

        public CPUBoostLevel CPUBoostLevel { get; set; } = CPUBoostLevel.Enabled;

        public FanProfile FanProfile { get; set; } = new();

        public int OEMPowerMode { get; set; } = 0xFF;
        public Guid OSPowerMode { get; set; } = Managers.OSPowerMode.BetterPerformance;

        public PowerProfile()
        { }

        public PowerProfile(string name, string description)
        {
            Name = name;
            Description = description;

            // Remove any invalid characters from the input
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string output = Regex.Replace(name, "[" + invalidChars + "]", string.Empty);
            output = output.Trim();

            FileName = output;
        }

        public string GetFileName()
        {
            return $"{FileName}.json";
        }

        public bool IsDefault()
        {
            return Default;
        }

        public override string ToString()
        {
            return $"{Name} - {Description}";
        }
    }
}
