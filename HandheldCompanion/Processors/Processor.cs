using HandheldCompanion.Managers;
using System.Collections.Generic;
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

    protected Dictionary<PowerType, int> m_Limits = new();

    protected Dictionary<string, float> m_Misc = new();
    protected Dictionary<PowerType, int> m_PrevLimits = new();
    protected Dictionary<string, float> m_PrevMisc = new();
    protected Dictionary<PowerType, float> m_PrevValues = new();

    protected Dictionary<PowerType, float> m_Values = new();

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
        // write default miscs
        processor.m_Misc["gfx_clk"] = processor.m_PrevMisc["gfx_clk"] = 0;

        return processor;
    }

    public virtual void Initialize()
    {
        StatusChanged?.Invoke(CanChangeTDP, CanChangeGPU);
        Initialized?.Invoke(this);

        // deprecated, we're using LibreHardwareMonitor to provide values and limits
        /*
        if (CanChangeTDP)
            updateTimer.Start();
        */
    }

    public virtual void Stop()
    {
        // deprecated, we're using LibreHardwareMonitor to provide values and limits
        /*
        if (CanChangeTDP)
            updateTimer.Stop();
        */
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

    protected virtual void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        // search for limit changes
        foreach (var pair in m_Limits)
        {
            if (m_PrevLimits[pair.Key] == pair.Value)
                continue;

            LimitChanged?.Invoke(pair.Key, pair.Value);

            m_PrevLimits[pair.Key] = pair.Value;
        }

        // search for value changes
        foreach (var pair in m_Values)
        {
            if (m_PrevValues[pair.Key] == pair.Value)
                continue;

            ValueChanged?.Invoke(pair.Key, pair.Value);

            m_PrevValues[pair.Key] = pair.Value;
        }

        // search for misc changes
        foreach (var pair in m_Misc)
        {
            if (m_PrevMisc[pair.Key] == pair.Value)
                continue;

            MiscChanged?.Invoke(pair.Key, pair.Value);

            m_PrevMisc[pair.Key] = pair.Value;
        }
    }

    #region events

    public event LimitChangedHandler LimitChanged;

    public delegate void LimitChangedHandler(PowerType type, int limit);

    public event ValueChangedHandler ValueChanged;

    public delegate void ValueChangedHandler(PowerType type, float value);

    public event GfxChangedHandler MiscChanged;

    public delegate void GfxChangedHandler(string misc, float value);

    public event StatusChangedHandler StatusChanged;

    public delegate void StatusChangedHandler(bool CanChangeTDP, bool CanChangeGPU);

    public event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler(Processor processor);

    #endregion
}