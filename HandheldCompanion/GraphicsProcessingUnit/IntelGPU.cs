using HandheldCompanion.IGCL;
using SharpDX.Direct3D9;
using System;
using System.Threading;
using System.Timers;
using static HandheldCompanion.IGCL.IGCLBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class IntelGPU : GPU
    {
        #region events
        public event EnduranceGamingStateEventHandler EnduranceGamingState;
        public delegate void EnduranceGamingStateEventHandler(bool Supported, ctl_3d_endurance_gaming_control_t Control, ctl_3d_endurance_gaming_mode_t Mode);
        #endregion

        private bool prevEnduranceGamingSupport;
        private ctl_3d_endurance_gaming_control_t prevEGControl = new();
        private ctl_3d_endurance_gaming_mode_t prevEGMode = new();

        protected ctl_telemetry_data TelemetryData = new();

        public override bool HasIntegerScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasIntegerScalingSupport(deviceIdx, 0), false);
        }

        public override bool HasGPUScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasGPUScalingSupport(deviceIdx, 0), false);
        }

        public override bool HasScalingModeSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasGPUScalingSupport(deviceIdx, 0), false);
        }

        public override bool GetGPUScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetGPUScaling(deviceIdx, 0), false);
        }

        public override bool GetImageSharpening()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetImageSharpening(deviceIdx, 0), false);
        }

        public override int GetImageSharpeningSharpness()
        {
            if (!IsInitialized)
                return 0;

            return Execute(() => IGCLBackend.GetImageSharpeningSharpness(deviceIdx, 0), 0);
        }

        public override bool GetIntegerScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetIntegerScaling(deviceIdx), false);
        }

        // GPUScaling can't be disabled on Intel GPU ?
        public override bool SetGPUScaling(bool enabled)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetGPUScaling(deviceIdx, 0), false);
        }

        public override bool SetImageSharpening(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetImageSharpening(deviceIdx, 0, enable), false);
        }

        public override bool SetImageSharpeningSharpness(int sharpness)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetImageSharpeningSharpness(deviceIdx, 0, sharpness), false);
        }

        public override bool SetScalingMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetScalingMode(deviceIdx, 0, mode), false);
        }

        public override bool SetIntegerScaling(bool enabled, byte type)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetIntegerScaling(deviceIdx, enabled, type), false);
        }
        
        // helper to test whether enumValue is supported:
        bool IsSupported<T>(uint mask, T enumValue) where T : Enum
        {
            int idx = Convert.ToInt32(enumValue);
            return ((mask >> idx) & 1) != 0;
        }

        public bool HasEnduranceGaming(out bool autoSupported, out bool onSupported, out bool offSupported)
        {
            autoSupported = false;
            onSupported = false;
            offSupported = false;

            if (!IsInitialized)
                return false;

            ctl_endurance_gaming_caps_t caps = GetEnduranceGamingCapacities();
            ctl_3d_endurance_gaming_control_t supportedControls = (ctl_3d_endurance_gaming_control_t)caps.EGControlCaps.SupportedTypes;
            ctl_3d_endurance_gaming_mode_t supportedModes = (ctl_3d_endurance_gaming_mode_t)caps.EGModeCaps.SupportedTypes;

            offSupported = IsSupported((uint)supportedControls, ctl_3d_endurance_gaming_control_t.OFF);
            onSupported = IsSupported((uint)supportedControls, ctl_3d_endurance_gaming_control_t.ON);
            autoSupported = IsSupported((uint)supportedControls, ctl_3d_endurance_gaming_control_t.AUTO);

            return autoSupported || onSupported;
        }

        public ctl_endurance_gaming_caps_t GetEnduranceGamingCapacities()
        {
            if (!IsInitialized)
                return new();

            ctl_endurance_gaming_caps_t caps = new ctl_endurance_gaming_caps_t();
            return Execute(() => IGCLBackend.GetEnduranceGamingCapacities(deviceIdx), new());
        }

        public bool SetEnduranceGaming(ctl_3d_endurance_gaming_control_t control, ctl_3d_endurance_gaming_mode_t mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetEnduranceGaming(
                deviceIdx,
                control,
                mode), false);
        }

        public ctl_endurance_gaming_t GetEnduranceGaming()
        {
            if (!IsInitialized)
                return new();

            return Execute(() => IGCLBackend.GetEnduranceGaming(deviceIdx), new());
        }

        private ctl_telemetry_data GetTelemetry()
        {
            if (!IsInitialized)
                return TelemetryData;

            return Execute(() =>
            {
                return IGCLBackend.GetTelemetry(deviceIdx);
            }, TelemetryData);
        }

        public override bool HasClock()
        {
            return TelemetryData.GpuCurrentClockFrequencySupported;
        }

        public override float GetClock()
        {
            return (float)TelemetryData.GpuCurrentClockFrequencyValue;
        }

        public override bool HasLoad()
        {
            return TelemetryData.GlobalActivitySupported;
        }

        public override float GetLoad()
        {
            return (float)TelemetryData.GlobalActivityValue;
        }

        public override bool HasPower()
        {
            return TelemetryData.GpuEnergySupported;
        }

        public override float GetPower()
        {
            return (float)TelemetryData.GpuEnergyValue;
        }

        public override bool HasTemperature()
        {
            return TelemetryData.GpuCurrentTemperatureSupported;
        }

        public override float GetTemperature()
        {
            return (float)TelemetryData.GpuCurrentTemperatureValue;
        }

        public IntelGPU(AdapterInformation adapterInformation) : base(adapterInformation)
        {
            deviceIdx = GetDeviceIdx(adapterInformation.Details.Description);
            if (deviceIdx == -1)
                return;

            IsInitialized = true;

            // pull telemetry once
            TelemetryData = IGCLBackend.GetTelemetry(deviceIdx);

            UpdateTimer = new Timer(UpdateInterval)
            {
                AutoReset = true
            };
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

            TelemetryTimer = new Timer(TelemetryInterval)
            {
                AutoReset = true
            };
            TelemetryTimer.Elapsed += TelemetryTimer_Elapsed;
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (halting)
                return;

            if (Monitor.TryEnter(updateLock))
            {
                try
                {
                    ctl_endurance_gaming_t EnduranceGaming = new();
                    ctl_endurance_gaming_caps_t EnduranceGamingCaps = new();

                    bool EnduranceGamingOff = false;
                    bool EnduranceGamingOn = false;
                    bool EnduranceGamingAuto = false;

                    try
                    {
                        bool EnduranceGamingSupport = HasEnduranceGaming(out EnduranceGamingOff, out EnduranceGamingOn, out EnduranceGamingAuto);
                        if (EnduranceGamingSupport)
                        {
                            EnduranceGaming = GetEnduranceGaming();
                            EnduranceGamingCaps = GetEnduranceGamingCapacities();
                        }

                        // raise event
                        if (EnduranceGamingSupport != prevEnduranceGamingSupport || EnduranceGaming.EGControl != prevEGControl || EnduranceGaming.EGMode != prevEGMode)
                            EnduranceGamingState?.Invoke(EnduranceGamingSupport, EnduranceGaming.EGControl, EnduranceGaming.EGMode);

                        prevEnduranceGamingSupport = EnduranceGamingSupport;
                        prevEGControl = EnduranceGaming.EGControl;
                        prevEGMode = EnduranceGaming.EGMode;
                    }
                    catch { }
                }
                catch { }
                finally
                {
                    Monitor.Exit(updateLock);
                }
            }
        }

        private void TelemetryTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (halting)
                return;

            if (Monitor.TryEnter(telemetryLock))
            {
                try
                {
                    TelemetryData = GetTelemetry();
                }
                catch { }
                finally
                {
                    Monitor.Exit(telemetryLock);
                }
            }
        }

        public override void Start()
        {
            if (!IsInitialized)
                return;

            base.Start();
        }

        public override void Stop()
        {
            base.Stop();
        }
    }
}
