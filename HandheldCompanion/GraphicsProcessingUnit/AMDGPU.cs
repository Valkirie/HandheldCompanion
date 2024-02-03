﻿using HandheldCompanion.ADLX;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

            return Execute(() => ADLXBackend.HasIntegerScalingSupport(0), false);
        }

        public override bool HasGPUScalingSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.HasGPUScalingSupport(0), false);
        }

        public override bool HasScalingModeSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.HasScalingModeSupport(0), false);
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

            return Execute(() => ADLXBackend.GetImageSharpening(0), false);
        }

        public override int GetImageSharpeningSharpness()
        {
            if (!IsInitialized)
                return -1;

            return Execute(() => ADLXBackend.GetImageSharpeningSharpness(0), -1);
        }

        public override bool GetIntegerScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetIntegerScaling(0), false);
        }

        public override bool GetGPUScaling()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetGPUScaling(0), false);
        }

        public override int GetScalingMode()
        {
            if (!IsInitialized)
                return -1;

            return Execute(() => ADLXBackend.GetScalingMode(0), -1);
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

            return Execute(() => ADLXBackend.SetImageSharpening(0, enable), false);
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
                    if (ADLXBackend.GetIntegerScaling(0))
                        ADLXBackend.SetIntegerScaling(0, false);

                    if (ADLXBackend.GetImageSharpening(0))
                        ADLXBackend.SetImageSharpening(0, false);
                }

                return ADLXBackend.SetRSR(enable);
            }, false);
        }

        public override bool SetImageSharpeningSharpness(int sharpness)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetImageSharpeningSharpness(0, sharpness), false);
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

                return ADLXBackend.SetIntegerScaling(0, enabled);
            }, false);
        }

        public override bool SetGPUScaling(bool enabled)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetGPUScaling(0, enabled), false);
        }

        public override bool SetScalingMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetScalingMode(0, mode), false);
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

        protected AdlxTelemetryData TelemetryData = new();

        public AMDGPU()
        {
            IsInitialized = ADLXBackend.IntializeAdlx();
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
                TelemetryData = ADLXBackend.GetTelemetryData();
                //Debug.WriteLine("W:{0}", TelemetryData.gpuPowerValue);

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

            // wait until the current ADLX tasks are completed
            while (!Monitor.TryEnter(updateLock) || !Monitor.TryEnter(telemetryLock))
                await Task.Delay(100);

            ADLXBackend.CloseAdlx();
        }

        private async void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (Monitor.TryEnter(updateLock))
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

                Monitor.Exit(updateLock);
            }
        }
    }
}
