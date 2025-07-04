﻿using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using System.Timers;

namespace HandheldCompanion.Processors;

public enum PowerType
{
    // long
    Slow = 0,
    Stapm = 1,
    Fast = 2,
    MsrSlow = 3,
    MsrFast = 4
}

public class Processor
{
    private static Processor processor;
    private static string Manufacturer;

    protected readonly Timer updateTimer = new() { Interval = 3000, AutoReset = true };

    protected bool UseOEM => (TDPMethod)ManagerFactory.settingsManager.GetInt("ConfigurableTDPMethod") == TDPMethod.OEM;

    protected bool HasOEMCPU => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.OEMCPU);
    protected bool HasOEMGPU => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.OEMGPU);

    public bool CanChangeTDP, CanChangeGPU;
    protected object updateLock = new();
    public bool IsInitialized;

    protected static string Name, ProcessorID;

    static Processor()
    {
        Name = MotherboardInfo.ProcessorName;
        ProcessorID = MotherboardInfo.ProcessorID;
        Manufacturer = MotherboardInfo.ProcessorManufacturer;
    }

    public static Processor GetCurrent()
    {
        if (processor is not null)
            return processor;

        switch (Manufacturer)
        {
            case "GenuineIntel":
                processor = new IntelProcessor();
                break;
            case "AuthenticAMD":
                processor = new AMDProcessor();
                break;
            default:
                LogManager.LogError("Failed to retrieve processor family: {0}", Manufacturer);
                break;
        }

        return processor;
    }

    public virtual void Stop()
    { }

    public virtual void SetTDPLimit(PowerType type, double limit, bool immediate = false, int result = 0)
    {
        LogManager.LogDebug("User requested {0} TDP limit: {1}, error code: {2}", type, (uint)limit, result);
    }

    public virtual void SetGPUClock(double clock, int result = 0)
    {
        /*
         * #define ADJ_ERR_FAM_UNSUPPORTED      -1
         * #define ADJ_ERR_SMU_TIMEOUT          -2
         * #define ADJ_ERR_SMU_UNSUPPORTED      -3
         * #define ADJ_ERR_SMU_REJECTED         -4
         * #define ADJ_ERR_MEMORY_ACCESS        -5
         */

        LogManager.LogDebug("User requested GPU clock: {0}, error code: {1}", clock, result);
    }
}