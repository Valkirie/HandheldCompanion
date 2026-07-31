using HandheldCompanion.ADLX;
using HandheldCompanion.Managers;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Text;
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
        public event RSRStateChangedEventHandler? RSRStateChanged;
        public delegate void RSRStateChangedEventHandler(bool Supported, bool Enabled, int Sharpness);

        public event AFMFStateChangedEventHandler? AFMFStateChanged;
        public delegate void AFMFStateChangedEventHandler(bool Supported, bool Enabled);

        public event AFMF21StateChangedEventHandler? AFMF21StateChanged;
        public delegate void AFMF21StateChangedEventHandler(bool AlgorithmSupported, int Algorithm, int SearchMode, int PerformanceMode, int FastMotionResponse);
        #endregion

        private bool prevRSRSupport = false;
        private bool prevRSR = false;
        private int prevRSRSharpness = -1;

        private bool prevAFMFSupport = false;
        private bool prevAFMF = false;

        private bool prevAFMFAlgorithmSupport = false;
        private int prevAFMFAlgorithm = -1;
        private int prevAFMFSearchMode = -1;
        private int prevAFMFPerformanceMode = -1;
        private int prevAFMFFastMotionResponse = -1;

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

        public bool HasAFMFSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(ADLXBackend.HasAFMFSupport, false);
        }

        public bool GetAFMF()
        {
            if (!IsInitialized)
                return false;

            return Execute(ADLXBackend.GetAFMF, false);
        }

        public bool GetAFMFAlgorithmSupport()
        {
            if (!IsInitialized)
                return false;

            return Execute(ADLXBackend.GetAFMFAlgorithmSupport, false);
        }

        public int GetAFMFAlgorithm()
        {
            if (!IsInitialized)
                return 0;

            return Execute(ADLXBackend.GetAFMFAlgorithm, 0);
        }

        public int GetAFMFSearchMode()
        {
            if (!IsInitialized)
                return 0;

            return Execute(ADLXBackend.GetAFMFSearchMode, 0);
        }

        public int GetAFMFPerformanceMode()
        {
            if (!IsInitialized)
                return 0;

            return Execute(ADLXBackend.GetAFMFPerformanceMode, 0);
        }

        public int GetAFMFFastMotionResponse()
        {
            if (!IsInitialized)
                return 0;

            return Execute(ADLXBackend.GetAFMFFastMotionResponse, 0);
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

        public bool SetAntiLag(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAntiLag(displayIdx, enable), false);
        }

        public bool GetAntiLag()
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.GetAntiLag(displayIdx), false);
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

            /*
            // mutually exclusive
            if (enable)
            {
                if (GetIntegerScaling())
                    SetIntegerScaling(false);

                if (GetImageSharpening())
                    SetImageSharpening(false);
            }
            */

            return Execute(() => ADLXBackend.SetRSR(enable), false);
        }

        public bool SetAFMF(bool enable)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAFMF(enable), false);
        }

        public bool SetAFMFAlgorithm(int algorithm)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAFMFAlgorithm(algorithm), false);
        }

        public bool SetAFMFSearchMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAFMFSearchMode(mode), false);
        }

        public bool SetAFMFPerformanceMode(int mode)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAFMFPerformanceMode(mode), false);
        }

        public bool SetAFMFFastMotionResponse(int response)
        {
            if (!IsInitialized)
                return false;

            return Execute(() => ADLXBackend.SetAFMFFastMotionResponse(response), false);
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

            /*
            // mutually exclusive
            if (enabled)
            {
                if (GetRSR())
                    SetRSR(false);
            }
            */

            return Execute(() => ADLXBackend.SetIntegerScaling(displayIdx, enabled), false);
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

        static AMDGPU()
        {
            ProcessTargets = new HashSet<string> { "RadeonSoftware", "cncmd" };
        }

        public AMDGPU(AdapterInformation adapterInformation) : base(adapterInformation)
        {
            ADLX_RESULT result = ADLX_RESULT.ADLX_FAIL;
            int adapterCount = 0;
            int UniqueId = 0;
            string dispName = string.Empty;
            string friendlyName = ManagerFactory.multimediaManager.GetAdapterFriendlyName(adapterInformation.Details.DeviceName);

            result = GetNumberOfDisplays(ref adapterCount);
            if (result != ADLX_RESULT.ADLX_OK)
                return;

            if (adapterCount == 0)
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

            // we couldn't pick a display by its name, pick first
            // todo: improve me
            if (displayIdx == -1)
                displayIdx = 0;

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

            UpdateTimer = new Timer(UpdateInterval)
            {
                AutoReset = true
            };
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

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

        protected override void UpdateSettings()
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

                        // raise event
                        if (GPUScalingSupport != prevGPUScalingSupport || GPUScaling != prevGPUScaling || ScalingMode != prevScalingMode)
                            base.OnGPUScalingChanged(GPUScalingSupport, GPUScaling, ScalingMode);
                    }
                    catch { }

                    try
                    {
                        // get RSR
                        bool RSRSupport = false;
                        bool RSR = false;
                        int RSRSharpness = GetRSRSharpness();

                        Task timeout = Task.Delay(TimeSpan.FromSeconds(2));
                        while (!timeout.IsCompleted && !RSRSupport)
                        {
                            RSRSupport = HasRSRSupport();
                            if (!RSRSupport) Thread.Sleep(1000);
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
                        // get AFMF
                        bool AFMFSupport = false;
                        bool AFMF = false;

                        Task timeout = Task.Delay(TimeSpan.FromSeconds(2));
                        while (!timeout.IsCompleted && !AFMFSupport)
                        {
                            AFMFSupport = HasAFMFSupport();
                            if (!AFMFSupport) Thread.Sleep(1000);
                        }
                        AFMF = GetAFMF();

                        if (AFMFSupport != prevAFMFSupport || AFMF != prevAFMF)
                        {
                            // raise event
                            AFMFStateChanged?.Invoke(AFMFSupport, AFMF);

                            prevAFMFSupport = AFMFSupport;
                            prevAFMF = AFMF;
                        }
                    }
                    catch { }

                    try
                    {
                        // get AFMF 2.1 sub-controls (only probed when basic AFMF is supported)
                        bool AFMFAlgorithmSupport = false;
                        int AFMFAlgorithm = 0;
                        int AFMFSearchMode = 0;
                        int AFMFPerformanceMode = 0;
                        int AFMFFastMotionResponse = 0;

                        if (prevAFMFSupport)
                        {
                            AFMFAlgorithmSupport = GetAFMFAlgorithmSupport();
                            if (AFMFAlgorithmSupport)
                            {
                                AFMFAlgorithm = GetAFMFAlgorithm();
                                AFMFSearchMode = GetAFMFSearchMode();
                                AFMFPerformanceMode = GetAFMFPerformanceMode();
                                AFMFFastMotionResponse = GetAFMFFastMotionResponse();
                            }
                        }

                        if (AFMFAlgorithmSupport != prevAFMFAlgorithmSupport ||
                            AFMFAlgorithm != prevAFMFAlgorithm ||
                            AFMFSearchMode != prevAFMFSearchMode ||
                            AFMFPerformanceMode != prevAFMFPerformanceMode ||
                            AFMFFastMotionResponse != prevAFMFFastMotionResponse)
                        {
                            // raise event
                            AFMF21StateChanged?.Invoke(AFMFAlgorithmSupport, AFMFAlgorithm, AFMFSearchMode, AFMFPerformanceMode, AFMFFastMotionResponse);

                            prevAFMFAlgorithmSupport = AFMFAlgorithmSupport;
                            prevAFMFAlgorithm = AFMFAlgorithm;
                            prevAFMFSearchMode = AFMFSearchMode;
                            prevAFMFPerformanceMode = AFMFPerformanceMode;
                            prevAFMFFastMotionResponse = AFMFFastMotionResponse;
                        }
                    }
                    catch { }

                    try
                    {
                        // get gpu scaling and scaling mode
                        bool IntegerScalingSupport = false;
                        bool IntegerScaling = false;

                        Task timeout = Task.Delay(TimeSpan.FromSeconds(2));
                        while (!timeout.IsCompleted && !IntegerScalingSupport && GPUScaling)
                        {
                            IntegerScalingSupport = HasIntegerScalingSupport();
                            if (!IntegerScalingSupport) Thread.Sleep(1000);
                        }
                        IntegerScaling = GetIntegerScaling();

                        // raise event
                        if (IntegerScalingSupport != prevIntegerScalingSupport || IntegerScaling != prevIntegerScaling)
                            base.OnIntegerScalingChanged(IntegerScalingSupport, IntegerScaling);
                    }
                    catch { }

                    try
                    {
                        bool ImageSharpening = GetImageSharpening();
                        int ImageSharpeningSharpness = GetImageSharpeningSharpness();

                        // raise event
                        if (ImageSharpening != prevImageSharpening || ImageSharpeningSharpness != prevImageSharpeningSharpness)
                            base.OnImageSharpeningChanged(ImageSharpening, ImageSharpeningSharpness);
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

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (halting)
                return;

            UpdateSettings();
        }

        protected override void BusyTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Call the generic method to terminate conflicting processes.
            TerminateConflictingProcesses();

            base.BusyTimer_Elapsed(sender, e);
        }
    }
}
