using HandheldCompanion.Devices;
using HandheldCompanion.Shared;
using LibreHardwareMonitor.PawnIo;
using System;

namespace HandheldCompanion.Processors
{
    public class AMDProcessor : Processor
    {
        private readonly object _smuLock = new();
        private RyzenSmu _smu;
        private bool _smuReady;

        public AMDProcessor()
        {
            try
            {
                _smu = new RyzenSmu();
                _smuReady = true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("PawnIO/RyzenSMU init failed: {0}", ex.Message);
                _smuReady = false;
            }

            CanChangeTDP = _smuReady || HasOEMCPU;
            CanChangeGPU = _smuReady || HasOEMGPU;

            IsInitialized = (CanChangeTDP || CanChangeGPU) && (_smuReady || HasOEMCPU || HasOEMGPU);
        }

        public override void SetTDPLimit(PowerType type, double limit, bool immediate, int result)
        {
            lock (_smuLock)
            {
                if (!CanChangeTDP)
                    return;

                if (HasOEMCPU && UseOEM)
                {
                    var device = IDevice.GetCurrent();
                    switch (type)
                    {
                        case PowerType.Slow: device.set_long_limit((int)limit); break;
                        case PowerType.Fast: device.set_short_limit((int)limit); break;
                    }
                    base.SetTDPLimit(type, limit, immediate, result);
                    return;
                }

                if (!_smuReady)
                    return;

                // HC UI is in W; SMU ioctls expect mW
                uint mw = (uint)Math.Round(limit * 1000.0);

                bool ok = type switch
                {
                    PowerType.Fast => _smu.SetPptFastLimit(mw),
                    PowerType.Slow => _smu.SetPptSlowLimit(mw),
                    PowerType.Stapm => _smu.SetStapmLimit(mw),
                    _ => false
                };

                result = ok ? 0 : unchecked((int)0x80004005);
                base.SetTDPLimit(type, limit, immediate, result);
            }
        }

        public override void SetGPUClock(double clock, int result)
        {
            lock (_smuLock)
            {
                if (!CanChangeGPU)
                    return;

                var device = IDevice.GetCurrent();
                bool restore = (clock == 12750);

                if (HasOEMGPU)
                {
                    device.set_min_gfxclk_freq((uint)(restore ? device.GfxClock[0] : clock));
                    device.set_max_gfxclk_freq((uint)(restore ? device.GfxClock[1] : clock));
                    base.SetGPUClock(clock, 0);
                    return;
                }

                if (!_smuReady)
                    return;

                bool ok = _smu.SetGfxClock((uint)clock) || (_smu.SetMinGfxClock((uint)clock) & _smu.SetMaxGfxClock((uint)clock));

                result = ok ? 0 : unchecked((int)0x80004005);
                base.SetGPUClock(clock, result);
            }
        }
    }
}
