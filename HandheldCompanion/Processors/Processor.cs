using HandheldCompanion.Managers;
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

    public bool CanChangeTDP, CanChangeGPU;
    protected object IsBusy = new();
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
        }

        return processor;
    }

    public virtual void Initialize()
    {
        StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
        Initialized?.Invoke(this);
    }

    public virtual void Stop()
    {
    }

    public virtual void SetTDPLimit(PowerType type, double limit, bool immediate = false, int result = 0)
    {
        if (!immediate)
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

    #region events

    public event StatusChangedHandler StatusChanged;

    public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

    public event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler(Processor processor);

    #endregion
}