using System.Threading;
using System.Timers;
using HandheldCompanion.IGCL;
using static HandheldCompanion.IGCL.IGCLBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class IntelGPU : GPU
    {
        #region events
        #endregion

        public override bool HasIntegerScalingSupport() => Execute(() => IGCLBackend.HasIntegerScalingSupport(IGCLBackend.deviceIdx, 0), false);
        public override bool HasGPUScalingSupport() => Execute(() => IGCLBackend.HasGPUScalingSupport(IGCLBackend.deviceIdx, 0), false);
        public override bool HasScalingModeSupport() => Execute(() => IGCLBackend.HasGPUScalingSupport(IGCLBackend.deviceIdx, 0), false);

        public override bool GetGPUScaling() => Execute(() => IGCLBackend.GetGPUScaling(IGCLBackend.deviceIdx, 0), false);
        public override bool GetImageSharpening() => Execute(() => IGCLBackend.GetImageSharpening(IGCLBackend.deviceIdx, 0), false);
        public int GetImageSharpeningSharpness() => Execute(() => IGCLBackend.GetImageSharpeningSharpness(IGCLBackend.deviceIdx, 0), 0);
        public override bool GetIntegerScaling() => Execute(() => IGCLBackend.GetIntegerScaling(IGCLBackend.deviceIdx), false);

        // GPUScaling can't be disabled on Intel GPU ?
        public override bool SetGPUScaling(bool enabled) => Execute(() => IGCLBackend.SetGPUScaling(IGCLBackend.deviceIdx, 0), false);
        public override bool SetImageSharpening(bool enable) => Execute(() => IGCLBackend.SetImageSharpening(IGCLBackend.deviceIdx, 0, enable), false);
        public override bool SetImageSharpeningSharpness(int sharpness) => Execute(() => IGCLBackend.SetImageSharpeningSharpness(IGCLBackend.deviceIdx, 0, sharpness), false);
        public override bool SetScalingMode(int mode) => Execute(() => IGCLBackend.SetScalingMode(IGCLBackend.deviceIdx, 0, mode), false);
        public override bool SetIntegerScaling(bool enabled, byte type) => Execute(() => IGCLBackend.SetIntegerScaling(IGCLBackend.deviceIdx, enabled, type), false);

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
            IGCLBackend.Initialize();
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

        public override void Stop()
        {
            IGCLBackend.Terminate();
            base.Stop();
        }
    }
}
