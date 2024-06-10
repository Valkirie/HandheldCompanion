using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using Device = System.Tuple<string, ulong, ulong, uint[]>;

namespace HandheldCompanion.Helpers
{
    internal class VangoghGPU : IDisposable
    {
        public static readonly Device[] SupportedDevices =
        {
            // SteamDeck LCD
            new Device("AMD Custom GPU 0405", 0x80300000, 0x8037ffff, new uint[] { 0x43F3900, 0x43F3C05, 0x43F3E00, 0x063F0F00 }),

            // SteamDeck OLED
            new Device("AMD Custom GPU 0932", 0x80500000, 0x8057ffff, new uint[] { 0x063F0F00 }),
            new Device("AMD Custom GPU 0932", 0x80600000, 0x8067ffff, new uint[] { 0x063F0E00, 0x063F0A00 }),

            // SteamDeck unofficial APU drivers
            // https://sourceforge.net/projects/amernimezone/files/Release%20Polaris-Vega-Navi/AMD%20SOC%20Driver%20Variant/
            new Device("AMD Radeon 670M", 0x80300000, 0x8037ffff, new uint[] { 0x43F3900, 0x43F3C05, 0x43F3E00 }),
            new Device("AMD Radeon RX 670 Graphics", 0x80300000, 0x8037ffff, new uint[] { 0x43F3900, 0x43F3C05, 0x43F3E00 }),
        };

        private static Device? DetectedDevice;

        public static bool IsSupported
        {
            get { return DetectedDevice != null; }
        }

        public static VangoghGPU? Open()
        {
            if (DetectedDevice is null)
                return null;

            return Open(DetectedDevice);
        }

        public static VangoghGPU? Open(Device device)
        {
            if (device is null)
                return null;

            return OpenMMIO(new IntPtr((long)device.Item2), (uint)(device.Item3 - device.Item2 + 1));
        }

        public enum DetectionStatus
        {
            Detected,
            Retryable,
            NotDetected
        }

        public static DetectionStatus Detect()
        {
            var discoveredDevices = new Dictionary<string, string>();

            foreach (var pnp in DeviceManager.GetDevices(DeviceManager.GUID_DISPLAY) ?? new string[0])
            {
                // Properly support many devices with the same name (pick the first one)
                var name = DeviceManager.GetDeviceDesc(pnp);
                if (name is not null && !discoveredDevices.ContainsKey(name))
                    discoveredDevices[name] = pnp;
            }

            foreach (var device in SupportedDevices)
            {
                var deviceName = device.Item1;

                if (!discoveredDevices.ContainsKey(deviceName))
                {
                    LogManager.LogDebug("GPU: {0}: Not matched.", deviceName);
                    continue;
                }

                var devicePNP = discoveredDevices[deviceName];
                var ranges = DeviceManager.GetDeviceMemResources(devicePNP);
                if (ranges is null)
                {
                    LogManager.LogDebug("GPU: {0}: {1}: No memory ranges", deviceName, devicePNP);
                    continue;
                }
                var expectedRange = new Tuple<UIntPtr, UIntPtr>(new UIntPtr(device.Item2), new UIntPtr(device.Item3));
                if (!ranges.Contains(expectedRange))
                {
                    LogManager.LogDebug("GPU: {0}: {1}: Memory range not found: {2}",
                        deviceName,
                        devicePNP,
                        String.Join(",", ranges.Select((item) => item.ToString()))
                    );
                    continue;
                }

                using (var gpu = Open(device))
                {
                    if (gpu is null)
                    {
                        LogManager.LogDebug("GPU: {0}: {1}: Failed to open.", deviceName, devicePNP);
                        continue;
                    }

                    var smuVersion = gpu.SMUVersion;
                    if (!device.Item4.Contains(smuVersion))
                    {
                        // Silence SMU_Version = 0 since it happens fairly often
                        if (smuVersion != 0)
                            LogManager.LogDebug("GPU: {0}: {1}: SMU not supported: {2:X8} (IO: {3})", deviceName, devicePNP, smuVersion, expectedRange);

                        continue;
                    }

                    LogManager.LogInformation("GPU: {0}: Matched!", deviceName);
                    DetectedDevice = device;
                    return DetectionStatus.Detected;
                }
            }
            DetectedDevice = null;
            return DetectionStatus.Detected;
        }

        // Addresses:
        // drivers/gpu/drm/amd/include/vangogh_ip_offset.h => MP1_BASE => 0x00016000
        // drivers/gpu/drm/amd/pm/swsmu/smu_cmn.c => mmMP1_SMN_C2PMSG_* => 0x0282/0x0292/0x029a
        // drivers/gpu/drm/amd/include/asic_reg/nbio/nbio_7_4_offset.h => mmPCIE_INDEX2/mmPCIE_DATA2 => 0x000e/0x000f
        //
        // Messages:
        // drivers/gpu/drm/amd/pm/inc/smu_v11_5_ppsmc.h

        private RyzenSMU smu;

        ~VangoghGPU()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            if (smu != null)
                smu.Dispose();
        }

        private static VangoghGPU? OpenMMIO(IntPtr mmioAddress, uint mmioSize)
        {
            var gpu = new VangoghGPU
            {
                smu = new RyzenSMU()
                {
                    MMIO_ADDR = mmioAddress,
                    MMIO_SIZE = mmioSize,
                    RES_ADDR = 0x0001629A * 4,
                    MSG_ADDR = 0x00016282 * 4,
                    PARAM_ADDR = 0x00016292 * 4,
                    INDEX_ADDR = 0xE * 4,
                    DATA_ADDR = 0xF * 4
                }
            };

            if (!gpu.smu.Open())
                return null;

            return gpu;
        }

        public UInt32 SMUVersion
        {
            get { return getValue(Message.PPSMC_MSG_GetSmuVersion); }
        }

        public UInt32 IfVersion
        {
            get { return getValue(Message.PPSMC_MSG_GetDriverIfVersion); }
        }

        public Features SmuFeatures
        {
            get
            {
                UInt64 low = getValue(Message.PPSMC_MSG_GetEnabledSmuFeatures, 0);
                UInt64 high = getValue(Message.PPSMC_MSG_GetEnabledSmuFeatures, 1);
                return (Features)((high << 32) | low);
            }
        }

        const uint MIN_TDP = 3000;
        const uint MAX_TDP = 21000;

        public uint SlowTDP
        {
            get { return getValue(Message.PPSMC_MSG_GetSlowPPTLimit); }
            set { setValue(Message.PPSMC_MSG_SetSlowPPTLimit, value, MIN_TDP, MAX_TDP); }
        }

        public uint FastTDP
        {
            get { return getValue(Message.PPSMC_MSG_GetFastPPTLimit); }
            set { setValue(Message.PPSMC_MSG_SetFastPPTLimit, value, MIN_TDP, MAX_TDP); }
        }

        public uint GfxClock
        {
            get { return getValue(Message.PPSMC_MSG_GetGfxclkFrequency); }
        }

        public uint FClock
        {
            get { return getValue(Message.PPSMC_MSG_GetFclkFrequency); }
        }

        const uint MIN_CPU_CLOCK = 1400;
        const uint MAX_CPU_CLOCK = 4000;

        public uint MinCPUClock
        {
            set { setCPUValue(Message.PPSMC_MSG_SetSoftMinCclk, value, MIN_CPU_CLOCK, MAX_CPU_CLOCK); }
        }

        public uint MaxCPUClock
        {
            set { setCPUValue(Message.PPSMC_MSG_SetSoftMaxCclk, value, MIN_CPU_CLOCK, MAX_CPU_CLOCK); }
        }

        const uint MIN_GFX_CLOCK = 200;
        const uint MAX_GFX_CLOCK = 1900;

        public uint HardMinGfxClock
        {
            set { setValue(Message.PPSMC_MSG_SetHardMinGfxClk, value, MIN_GFX_CLOCK, MAX_GFX_CLOCK); }
        }

        public uint SoftMinGfxClock
        {
            set { setValue(Message.PPSMC_MSG_SetSoftMinGfxclk, value, MIN_GFX_CLOCK, MAX_GFX_CLOCK); }
        }

        public uint SoftMaxGfxClock
        {
            set { setValue(Message.PPSMC_MSG_SetSoftMaxGfxClk, value, MIN_GFX_CLOCK, MAX_GFX_CLOCK); }
        }

        public Dictionary<string, uint> All
        {
            get
            {
                var dict = new Dictionary<string, uint>();

                foreach (var key in ValuesGetters)
                {
                    if (!this.smu.SendMsg(key, 0, out var value))
                        continue;

                    var keyString = key.ToString().Replace("PPSMC_MSG_Get", "");
                    dict[keyString] = value;
                }

                return dict;
            }
        }

        private void setCPUValue(Message msg, uint value, uint min = UInt32.MinValue, uint max = UInt32.MaxValue)
        {
            // TODO: Hardcode CPUs
            for (uint i = 0; i < 4; i++)
            {
                setValue(msg, value | (i << 20), min, max, (1 << 20) - 1);
            }
        }

        private uint getValue(Message msg, UInt32 param = 0)
        {
            this.smu.SendMsg(msg, param, out var value);
            return value;
        }

        private void setValue(Message msg, uint value, uint min = UInt32.MinValue, uint max = UInt32.MaxValue, uint clampMask = uint.MaxValue)
        {
            this.smu.SendMsg(msg, Math.Clamp(value & clampMask, min, max) | (value & ~clampMask));
        }

        private readonly Message[] ValuesGetters = new Message[]
        {
            Message.PPSMC_MSG_GetGfxclkFrequency,
            Message.PPSMC_MSG_GetFclkFrequency,
            Message.PPSMC_MSG_GetPptLimit,
            Message.PPSMC_MSG_GetThermalLimit,
            Message.PPSMC_MSG_GetFastPPTLimit,
            Message.PPSMC_MSG_GetSlowPPTLimit,

            // Those values return PPSMC_Result_CmdRejectedPrereq
            Message.PPSMC_MSG_GetCurrentTemperature,
            Message.PPSMC_MSG_GetCurrentPower,
            Message.PPSMC_MSG_GetCurrentCurrent,
            Message.PPSMC_MSG_GetCurrentFreq,
            Message.PPSMC_MSG_GetCurrentVoltage,
            Message.PPSMC_MSG_GetAverageCpuActivity,
            Message.PPSMC_MSG_GetAverageGfxActivity,
            Message.PPSMC_MSG_GetAveragePower,
            Message.PPSMC_MSG_GetAverageTemperature
        };

        enum Result : byte
        {
            PPSMC_Result_OK = 0x1,
            PPSMC_Result_Failed = 0xFF,
            PPSMC_Result_UnknownCmd = 0xFE,
            PPSMC_Result_CmdRejectedPrereq = 0xFD,
            PPSMC_Result_CmdRejectedBusy = 0xFC
        }

        enum Tables : byte
        {
            TABLE_BIOS_IF = 0, // Called by BIOS
            TABLE_WATERMARKS = 1, // Called by DAL through VBIOS
            TABLE_CUSTOM_DPM = 2, // Called by Driver
            TABLE_SPARE1 = 3,
            TABLE_DPMCLOCKS = 4, // Called by Driver
            TABLE_SPARE2 = 5, // Called by Tools
            TABLE_MODERN_STDBY = 6, // Called by Tools for Modern Standby Log
            TABLE_SMU_METRICS = 7, // Called by Driver
            TABLE_COUNT = 8
        }

        enum Message : ushort
        {
            PPSMC_MSG_TestMessage = 0x1,
            PPSMC_MSG_GetSmuVersion = 0x2,
            PPSMC_MSG_GetDriverIfVersion = 0x3,
            PPSMC_MSG_EnableGfxOff = 0x4,
            PPSMC_MSG_DisableGfxOff = 0x5,
            PPSMC_MSG_PowerDownIspByTile = 0x6, // ISP is power gated by default
            PPSMC_MSG_PowerUpIspByTile = 0x7,
            PPSMC_MSG_PowerDownVcn = 0x8, // VCN is power gated by default
            PPSMC_MSG_PowerUpVcn = 0x9,
            PPSMC_MSG_RlcPowerNotify = 0xA,
            PPSMC_MSG_SetHardMinVcn = 0xB, // For wireless display
            PPSMC_MSG_SetSoftMinGfxclk = 0xC, //Sets SoftMin for GFXCLK. Arg is in MHz
            PPSMC_MSG_ActiveProcessNotify = 0xD,
            PPSMC_MSG_SetHardMinIspiclkByFreq = 0xE,
            PPSMC_MSG_SetHardMinIspxclkByFreq = 0xF,
            PPSMC_MSG_SetDriverDramAddrHigh = 0x10,
            PPSMC_MSG_SetDriverDramAddrLow = 0x11,
            PPSMC_MSG_TransferTableSmu2Dram = 0x12,
            PPSMC_MSG_TransferTableDram2Smu = 0x13,
            PPSMC_MSG_GfxDeviceDriverReset = 0x14, //mode 2 reset during TDR
            PPSMC_MSG_GetEnabledSmuFeatures = 0x15,
            PPSMC_MSG_spare1 = 0x16,
            PPSMC_MSG_SetHardMinSocclkByFreq = 0x17,
            PPSMC_MSG_SetSoftMinFclk = 0x18, //Used to be PPSMC_MSG_SetMinVideoFclkFreq
            PPSMC_MSG_SetSoftMinVcn = 0x19,
            PPSMC_MSG_EnablePostCode = 0x1A,
            PPSMC_MSG_GetGfxclkFrequency = 0x1B,
            PPSMC_MSG_GetFclkFrequency = 0x1C,
            PPSMC_MSG_AllowGfxOff = 0x1D,
            PPSMC_MSG_DisallowGfxOff = 0x1E,
            PPSMC_MSG_SetSoftMaxGfxClk = 0x1F,
            PPSMC_MSG_SetHardMinGfxClk = 0x20,
            PPSMC_MSG_SetSoftMaxSocclkByFreq = 0x21,
            PPSMC_MSG_SetSoftMaxFclkByFreq = 0x22,
            PPSMC_MSG_SetSoftMaxVcn = 0x23,
            PPSMC_MSG_spare2 = 0x24,
            PPSMC_MSG_SetPowerLimitPercentage = 0x25,
            PPSMC_MSG_PowerDownJpeg = 0x26,
            PPSMC_MSG_PowerUpJpeg = 0x27,
            PPSMC_MSG_SetHardMinFclkByFreq = 0x28,
            PPSMC_MSG_SetSoftMinSocclkByFreq = 0x29,
            PPSMC_MSG_PowerUpCvip = 0x2A,
            PPSMC_MSG_PowerDownCvip = 0x2B,
            PPSMC_MSG_GetPptLimit = 0x2C,
            PPSMC_MSG_GetThermalLimit = 0x2D,
            PPSMC_MSG_GetCurrentTemperature = 0x2E,
            PPSMC_MSG_GetCurrentPower = 0x2F,
            PPSMC_MSG_GetCurrentVoltage = 0x30,
            PPSMC_MSG_GetCurrentCurrent = 0x31,
            PPSMC_MSG_GetAverageCpuActivity = 0x32,
            PPSMC_MSG_GetAverageGfxActivity = 0x33,
            PPSMC_MSG_GetAveragePower = 0x34,
            PPSMC_MSG_GetAverageTemperature = 0x35,
            PPSMC_MSG_SetAveragePowerTimeConstant = 0x36,
            PPSMC_MSG_SetAverageActivityTimeConstant = 0x37,
            PPSMC_MSG_SetAverageTemperatureTimeConstant = 0x38,
            PPSMC_MSG_SetMitigationEndHysteresis = 0x39,
            PPSMC_MSG_GetCurrentFreq = 0x3A,
            PPSMC_MSG_SetReducedPptLimit = 0x3B,
            PPSMC_MSG_SetReducedThermalLimit = 0x3C,
            PPSMC_MSG_DramLogSetDramAddr = 0x3D,
            PPSMC_MSG_StartDramLogging = 0x3E,
            PPSMC_MSG_StopDramLogging = 0x3F,
            PPSMC_MSG_SetSoftMinCclk = 0x40,
            PPSMC_MSG_SetSoftMaxCclk = 0x41,
            PPSMC_MSG_SetDfPstateActiveLevel = 0x42,
            PPSMC_MSG_SetDfPstateSoftMinLevel = 0x43,
            PPSMC_MSG_SetCclkPolicy = 0x44,
            PPSMC_MSG_DramLogSetDramAddrHigh = 0x45,
            PPSMC_MSG_DramLogSetDramBufferSize = 0x46,
            PPSMC_MSG_RequestActiveWgp = 0x47,
            PPSMC_MSG_QueryActiveWgp = 0x48,
            PPSMC_MSG_SetFastPPTLimit = 0x49,
            PPSMC_MSG_SetSlowPPTLimit = 0x4A,
            PPSMC_MSG_GetFastPPTLimit = 0x4B,
            PPSMC_MSG_GetSlowPPTLimit = 0x4C,
            PPSMC_Message_Count = 0x4D,
        }

        [Flags]
        public enum Features : UInt64
        {
            CCLK_DPM_BIT = 0,
            FAN_CONTROLLER_BIT = 1,
            DATA_CALCULATION_BIT = 2,
            PPT_BIT = 3,
            TDC_BIT = 4,
            THERMAL_BIT = 5,
            FIT_BIT = 6,
            EDC_BIT = 7,
            PLL_POWER_DOWN_BIT = 8,
            ULV_BIT = 9,
            VDDOFF_BIT = 10,
            VCN_DPM_BIT = 11,
            CSTATE_BOOST_BIT = 12,
            FCLK_DPM_BIT = 13,
            SOCCLK_DPM_BIT = 14,
            MP0CLK_DPM_BIT = 15,
            LCLK_DPM_BIT = 16,
            SHUBCLK_DPM_BIT = 17,
            DCFCLK_DPM_BIT = 18,
            GFX_DPM_BIT = 19,
            DS_GFXCLK_BIT = 20,
            DS_SOCCLK_BIT = 21,
            DS_LCLK_BIT = 22,
            DS_DCFCLK_BIT = 23,
            DS_SHUBCLK_BIT = 24,
            GFX_TEMP_VMIN_BIT = 25,
            S0I2_BIT = 26,
            WHISPER_MODE_BIT = 27,
            DS_FCLK_BIT = 28,
            DS_SMNCLK_BIT = 29,
            DS_MP1CLK_BIT = 30,
            DS_MP0CLK_BIT = 31,
            SMU_LOW_POWER_BIT = 32,
            FUSE_PG_BIT = 33,
            GFX_DEM_BIT = 34,
            PSI_BIT = 35,
            PROCHOT_BIT = 36,
            CPUOFF_BIT = 37,
            STAPM_BIT = 38,
            S0I3_BIT = 39,
            DF_CSTATES_BIT = 40,
            PERF_LIMIT_BIT = 41,
            CORE_DLDO_BIT = 42,
            RSMU_LOW_POWER_BIT = 43,
            SMN_LOW_POWER_BIT = 44,
            THM_LOW_POWER_BIT = 45,
            SMUIO_LOW_POWER_BIT = 46,
            MP1_LOW_POWER_BIT = 47,
            DS_VCN_BIT = 48,
            CPPC_BIT = 49,
            OS_CSTATES_BIT = 50,
            ISP_DPM_BIT = 51,
            A55_DPM_BIT = 52,
            CVIP_DSP_DPM_BIT = 53,
            MSMU_LOW_POWER_BIT = 54,
            SOC_VOLTAGE_MON_BIT = 55,
            ATHUB_PG_BIT = 56,
            ECO_DEEPCSTATE_BIT = 57,
            CC6_BIT = 58,
            GFX_EDC_BIT = 59
        }
    }
}
