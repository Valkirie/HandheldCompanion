using HandheldCompanion.IGCL;
using SharpDX.Direct3D9;
using System.Threading;
using System.Timers;
using static HandheldCompanion.IGCL.IGCLBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class IntelGPU : GPU
    {
        #region events
        #endregion

        protected new ctl_telemetry_data TelemetryData = new();

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
            TelemetryData = GetTelemetry();

            TelemetryTimer = new Timer(TelemetryInterval);
            TelemetryTimer.AutoReset = true;
            TelemetryTimer.Elapsed += TelemetryTimer_Elapsed;
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
