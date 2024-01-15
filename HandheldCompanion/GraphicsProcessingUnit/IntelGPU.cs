using HandheldCompanion.IGCL;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static HandheldCompanion.IGCL.IGCLBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class IntelGPU : GPU
    {
        #region events
        #endregion

        public override bool HasIntegerScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasIntegerScalingSupport(IGCLBackend.deviceIdx, 0), false);
        }

        public override bool HasGPUScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasGPUScalingSupport(IGCLBackend.deviceIdx, 0), false);
        }

        public override bool HasScalingModeSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.HasGPUScalingSupport(IGCLBackend.deviceIdx, 0), false);
        }

        public override bool GetGPUScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetGPUScaling(IGCLBackend.deviceIdx, 0), false);
        }

        public override bool GetImageSharpening()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetImageSharpening(IGCLBackend.deviceIdx, 0), false);
        }

        public override int GetImageSharpeningSharpness()
        {
            if (!IsInitialized)
                return 0;

            return Execute(() => IGCLBackend.GetImageSharpeningSharpness(IGCLBackend.deviceIdx, 0), 0);
        }

        public override bool GetIntegerScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.GetIntegerScaling(IGCLBackend.deviceIdx), false);
        }

        // GPUScaling can't be disabled on Intel GPU ?
        public override bool SetGPUScaling(bool enabled)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetGPUScaling(IGCLBackend.deviceIdx, 0), false);
        }

        public override bool SetImageSharpening(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetImageSharpening(IGCLBackend.deviceIdx, 0, enable), false);
        }

        public override bool SetImageSharpeningSharpness(int sharpness)
        {
            return Execute(() => IGCLBackend.SetImageSharpeningSharpness(IGCLBackend.deviceIdx, 0, sharpness), false);
        }

        public override bool SetScalingMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetScalingMode(IGCLBackend.deviceIdx, 0, mode), false);
        }

        public override bool SetIntegerScaling(bool enabled, byte type)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => IGCLBackend.SetIntegerScaling(IGCLBackend.deviceIdx, enabled, type), false);
        }

        public override float GetClock()
        {
            return (float)TelemetryData.GpuCurrentClockFrequencyValue;
        }

        public override float GetClock()
        {
            return (float)TelemetryData.GpuCurrentClockFrequencyValue;
        }

        public override float GetLoad()
        {
            return (float)TelemetryData.GlobalActivityValue;
        }

        public override float GetPower()
        {
            return (float)TelemetryData.GpuEnergyValue;
        }

        public override float GetTemperature()
        {
            return (float)TelemetryData.GpuCurrentTemperatureValue;
        }

        protected ctl_telemetry_data TelemetryData = new();

        public IntelGPU()
        {
            IsInitialized = IGCLBackend.Initialize();
            if (!IsInitialized)
                return;

            TelemetryTimer = new Timer(TelemetryInterval);
            TelemetryTimer.AutoReset = true;
            TelemetryTimer.Elapsed += TelemetryTimer_Elapsed;
        }

        private void TelemetryTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(telemetryLock))
            {
                TelemetryData = IGCLBackend.GetTelemetryData();

                Monitor.Exit(telemetryLock);
            }
        }

        public override void Start()
        {
            base.Start();
        }

        public override async void Stop()
        {
            base.Stop();

            // wait until the current IGCL tasks are completed
            while (!Monitor.TryEnter(updateLock) || !Monitor.TryEnter(telemetryLock))
                await Task.Delay(100);

            IGCLBackend.Terminate();
        }
    }
}
