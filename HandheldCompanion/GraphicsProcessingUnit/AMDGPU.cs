using HandheldCompanion.ADLX;
using HandheldCompanion.Managers;
using SharpDX.Direct3D9;
using System;
using System.Text;
using System.Threading;
using System.Timers;
using static HandheldCompanion.ADLX.ADLXBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class AMDGPU : GPU
    {
        #region events
        public event RSRStateChangedEventHandler RSRStateChanged;
        public delegate void RSRStateChangedEventHandler(bool Supported, bool Enabled, int Sharpness);
        #endregion

        private bool prevRSRSupport = false;
        private bool prevRSR = false;
        private int prevRSRSharpness = -1;

        protected new AdlxTelemetryData TelemetryData = new();

        public bool HasRSRSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(ADLXBackend.HasRSRSupport, false);
        }

        public override bool HasIntegerScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.HasIntegerScalingSupport(displayIdx), false);
        }

        public override bool HasGPUScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.HasGPUScalingSupport(displayIdx), false);
        }

        public override bool HasScalingModeSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.HasScalingModeSupport(displayIdx), false);
        }

        public bool GetRSR()
        {
            if (!IsInitialized)
                return false;

            return Execute(ADLXBackend.GetRSR, false);
        }

        public int GetRSRSharpness()
        {
            if (!IsInitialized)
                return -1;

            return Execute(ADLXBackend.GetRSRSharpness, -1);
        }

        public override bool GetImageSharpening()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetImageSharpening(deviceIdx), false);
        }

        public override int GetImageSharpeningSharpness()
        {
            if (!IsInitialized)
                return -1;

            return Execute(() => ADLXBackend.GetImageSharpeningSharpness(deviceIdx), -1);
        }

        public override bool GetIntegerScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetIntegerScaling(displayIdx), false);
        }

        public override bool GetGPUScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetGPUScaling(displayIdx), false);
        }

        public override int GetScalingMode()
        {
            if (!IsInitialized)
                return -1;

            return Execute(() => ADLXBackend.GetScalingMode(displayIdx), -1);
        }

        public bool SetRSRSharpness(int sharpness)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetRSRSharpness(sharpness), false);
        }

        public override bool SetImageSharpening(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetImageSharpening(deviceIdx, enable), false);
        }

        public bool SetRSR(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() =>
            {
                // mutually exclusive
                if (enable)
                {
                    if (ADLXBackend.GetIntegerScaling(displayIdx))
                        ADLXBackend.SetIntegerScaling(displayIdx, false);

                    if (ADLXBackend.GetImageSharpening(deviceIdx))
                        ADLXBackend.SetImageSharpening(deviceIdx, false);
                }

                return ADLXBackend.SetRSR(enable);
            }, false);
        }

        public override bool SetImageSharpeningSharpness(int sharpness)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetImageSharpeningSharpness(deviceIdx, sharpness), false);
        }

        public override bool SetIntegerScaling(bool enabled, byte type = 0)
        {
            if (!IsInitialized)
                return false;

            return Execute(() =>
            {
                // mutually exclusive
                if (enabled)
                {
                    if (ADLXBackend.GetRSR())
                        ADLXBackend.SetRSR(false);
                }

                return ADLXBackend.SetIntegerScaling(displayIdx, enabled);
            }, false);
        }

        public override bool SetGPUScaling(bool enabled)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetGPUScaling(displayIdx, enabled), false);
        }

        public override bool SetScalingMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetScalingMode(displayIdx, mode), false);
        }

        private AdlxTelemetryData GetTelemetry()
        {
            if (!IsInitialized)
                return TelemetryData;

            return Execute(() =>
            {
                ADLXBackend.GetAdlxTelemetry(deviceIdx, ref TelemetryData);
                return TelemetryData;
            }, TelemetryData);
        }

        public override float GetClock()
        {
            return (float)TelemetryData.gpuClockSpeedValue;
        }

        public override float GetLoad()
        {
            return (float)TelemetryData.gpuUsageValue;
        }

        public override float GetPower()
        {
            return (float)TelemetryData.gpuPowerValue;
        }

        public override float GetTemperature()
        {
            return (float)TelemetryData.gpuTemperatureValue;
        }

        public override float GetVRAMUsage()
        {
            return (float)TelemetryData.gpuVramValue;
        }

        public AMDGPU(AdapterInformation adapterInformation) : base(adapterInformation)
        {
            ADLX_RESULT result = ADLX_RESULT.ADLX_FAIL;
            int adapterCount = 0;
            int UniqueId = 0;
            string dispName = string.Empty;
            string friendlyName = MultimediaManager.GetDisplayFriendlyName(adapterInformation.Details.DeviceName);

            result = GetNumberOfDisplays(ref adapterCount);
            if (result != ADLX_RESULT.ADLX_OK)
                return;

            for (int idx = 0; idx < adapterCount; idx++)
            {
                StringBuilder displayName = new StringBuilder(256); // Assume display name won't exceed 255 characters

                // skip if failed to retrieve display
                result = GetDisplayName(idx, displayName, displayName.Capacity);
                if (result != ADLX_RESULT.ADLX_OK)
                    continue;

                // skip if display is not the one we're looking for
                if (!displayName.ToString().Equals(friendlyName))
                    continue;

                // update displayIdx
                displayIdx = idx;
                break;
            }

            if (displayIdx != -1)
            {
                // get the associated GPU UniqueId
                result = GetDisplayGPU(displayIdx, ref UniqueId);
                if (result == ADLX_RESULT.ADLX_OK)
                {
                    result = GetGPUIndex(UniqueId, ref deviceIdx);
                    if (result == ADLX_RESULT.ADLX_OK)
                        IsInitialized = true;
                }
            }

            if (!IsInitialized)
                return;

            UpdateTimer = new Timer(UpdateInterval);
            UpdateTimer.AutoReset = true;
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

            TelemetryTimer = new Timer(TelemetryInterval);
            TelemetryTimer.AutoReset = true;
            TelemetryTimer.Elapsed += TelemetryTimer_Elapsed;
        }

        private void TelemetryTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
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

        public override async void Stop()
        {
            base.Stop();
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
            {
                try
                {
                    bool GPUScaling = false;

                    try
                    {
                        // check for GPU Scaling support
                        // if yes, get GPU Scaling (bool)
                        bool GPUScalingSupport = HasGPUScalingSupport();
                        if (GPUScalingSupport)
                            GPUScaling = GetGPUScaling();

                        // check for Scaling Mode support
                        // if yes, get Scaling Mode (int)
                        bool ScalingSupport = HasScalingModeSupport();
                        int ScalingMode = 0;
                        if (ScalingSupport)
                            ScalingMode = GetScalingMode();

                        if (GPUScalingSupport != prevGPUScalingSupport || GPUScaling != prevGPUScaling || ScalingMode != prevScalingMode)
                        {
                            // raise event
                            base.OnGPUScalingChanged(GPUScalingSupport, GPUScaling, ScalingMode);

                            prevGPUScaling = GPUScaling;
                            prevScalingMode = ScalingMode;
                            prevGPUScalingSupport = GPUScalingSupport;
                        }
                    }
                    catch { }

                    try
                    {
                        // get rsr
                        bool RSRSupport = false;
                        bool RSR = false;
                        int RSRSharpness = GetRSRSharpness();

                        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(3));
                        while (DateTime.Now < timeout && !RSRSupport)
                        {
                            RSRSupport = HasRSRSupport();
                            Thread.Sleep(250);
                        }
                        RSR = GetRSR();

                        if (RSRSupport != prevRSRSupport || RSR != prevRSR || RSRSharpness != prevRSRSharpness)
                        {
                            // raise event
                            RSRStateChanged?.Invoke(RSRSupport, RSR, RSRSharpness);

                            prevRSRSupport = RSRSupport;
                            prevRSR = RSR;
                            prevRSRSharpness = RSRSharpness;
                        }
                    }
                    catch { }

                    try
                    {
                        // get gpu scaling and scaling mode
                        bool IntegerScalingSupport = false;
                        bool IntegerScaling = false;

                        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(3));
                        while (DateTime.Now < timeout && !IntegerScalingSupport)
                        {
                            IntegerScalingSupport = HasIntegerScalingSupport();
                            Thread.Sleep(250);
                        }
                        IntegerScaling = GetIntegerScaling();

                        if (IntegerScalingSupport != prevIntegerScalingSupport || IntegerScaling != prevIntegerScaling)
                        {
                            // raise event
                            base.OnIntegerScalingChanged(IntegerScalingSupport, IntegerScaling);

                            prevIntegerScalingSupport = IntegerScalingSupport;
                            prevIntegerScaling = IntegerScaling;
                        }
                    }
                    catch { }

                    try
                    {
                        bool ImageSharpening = GetImageSharpening();
                        int ImageSharpeningSharpness = GetImageSharpeningSharpness();

                        if (ImageSharpening != prevImageSharpening || ImageSharpeningSharpness != prevImageSharpeningSharpness)
                        {
                            // raise event
                            base.OnImageSharpeningChanged(ImageSharpening, ImageSharpeningSharpness);

                            prevImageSharpening = ImageSharpening;
                            prevImageSharpeningSharpness = ImageSharpeningSharpness;
                        }
                    }
                    catch { }
                }
                finally
                {
                    Monitor.Exit(updateLock);
                }
            }
        }
    }
}
