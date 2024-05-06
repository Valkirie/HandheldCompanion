using HandheldCompanion.ADLX;
using HandheldCompanion.Controls;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.IGCL;
using HandheldCompanion.Managers.Desktop;
using SharpDX.Direct3D9;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers
{
    public static class GPUManager
    {
        #region events
        public static event InitializedEventHandler? Initialized;
        public delegate void InitializedEventHandler(bool HasIGCL, bool HasADLX);

        public static event HookedEventHandler? Hooked;
        public delegate void HookedEventHandler(GPU GPU);

        public static event UnhookedEventHandler? Unhooked;
        public delegate void UnhookedEventHandler(GPU GPU);
        #endregion

        public static bool IsInitialized = false;
        public static bool IsLoaded_IGCL = false;
        public static bool IsLoaded_ADLX = false;

        private static GPU currentGPU = null;
        private static ConcurrentDictionary<AdapterInformation, GPU> DisplayGPU = new();

        private static void GPUConnect(GPU GPU)
        {
            // update current GPU
            currentGPU = GPU;

            GPU.ImageSharpeningChanged += CurrentGPU_ImageSharpeningChanged;
            GPU.GPUScalingChanged += CurrentGPU_GPUScalingChanged;
            GPU.IntegerScalingChanged += CurrentGPU_IntegerScalingChanged;

            if (GPU is AMDGPU)
            {
                ((AMDGPU)GPU).RSRStateChanged += CurrentGPU_RSRStateChanged;
            }
            else if (GPU is IntelGPU)
            {
                // do something
            }

            if (GPU.IsInitialized)
                GPU.Start();

            if (IsInitialized && GPU.IsInitialized)
                Hooked?.Invoke(GPU);
        }

        private static void GPUDisconnect(GPU gpu)
        {
            if (currentGPU == gpu)
                Unhooked?.Invoke(gpu);

            gpu.ImageSharpeningChanged -= CurrentGPU_ImageSharpeningChanged;
            gpu.GPUScalingChanged -= CurrentGPU_GPUScalingChanged;
            gpu.IntegerScalingChanged -= CurrentGPU_IntegerScalingChanged;

            if (gpu is AMDGPU)
            {
                ((AMDGPU)gpu).RSRStateChanged -= CurrentGPU_RSRStateChanged;
            }
            else if (gpu is IntelGPU)
            {
                // do something
            }

            gpu.Stop();
        }

        private static async void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            while (!DeviceManager.IsInitialized)
                await Task.Delay(250);

            try
            {
                AdapterInformation key = DisplayGPU.Keys.FirstOrDefault(GPU => GPU.Details.DeviceName == screen.PrimaryScreen.DeviceName);
                if (DisplayGPU.TryGetValue(key, out GPU gpu))
                {
                    // a new GPU was connected, disconnect from current gpu
                    if (currentGPU is not null && currentGPU != gpu)
                        GPUDisconnect(currentGPU);

                    // connect to new gpu
                    GPUConnect(gpu);
                }
            }
            catch
            {
                // AdapterInformation can't be null
            }
        }

        private static async void DeviceManager_DisplayAdapterArrived(AdapterInformation adapterInformation)
        {
            while (!IsInitialized)
                await Task.Delay(250);

            GPU newGPU = null;

            if (adapterInformation.Details.Description.Contains("Advanced Micro Devices") || adapterInformation.Details.Description.Contains("AMD"))
            {
                newGPU = new AMDGPU(adapterInformation);
            }
            else if (adapterInformation.Details.Description.Contains("Intel"))
            {
                newGPU = new IntelGPU(adapterInformation);
            }

            if (newGPU is null)
            {
                LogManager.LogError("Unsupported DisplayAdapter: {0}, VendorID:{1}, DeviceId:{2}", adapterInformation.Details.Description, adapterInformation.Details.VendorId, adapterInformation.Details.DeviceId);
                return;
            }

            if (!newGPU.IsInitialized)
            {
                LogManager.LogError("Failed to initialize DisplayAdapter: {0}, VendorID:{1}, DeviceId:{2}", adapterInformation.Details.Description, adapterInformation.Details.VendorId, adapterInformation.Details.DeviceId);
                return;
            }

            DisplayGPU.TryAdd(adapterInformation, newGPU);
        }

        private static void DeviceManager_DisplayAdapterRemoved(AdapterInformation adapterInformation)
        {
            if (DisplayGPU.TryRemove(adapterInformation, out GPU gpu))
            {
                GPUDisconnect(gpu);
                gpu.Dispose();
            }
        }

        public static GPU GetCurrent()
        {
            return currentGPU;
        }

        private static void CurrentGPU_RSRStateChanged(bool Supported, bool Enabled, int Sharpness)
        {
            if (!IsInitialized)
                return;

            // todo: use ProfileMager events
            Profile profile = ProfileManager.GetCurrent();
            AMDGPU amdGPU = (AMDGPU)currentGPU;

            if (Enabled != profile.RSREnabled)
                amdGPU.SetRSR(profile.RSREnabled);
            if (Sharpness != profile.RSRSharpness)
                amdGPU.SetRSRSharpness(profile.RSRSharpness);
        }

        private static void CurrentGPU_IntegerScalingChanged(bool Supported, bool Enabled)
        {
            if (!IsInitialized)
                return;

            // todo: use ProfileMager events
            Profile profile = ProfileManager.GetCurrent();

            if (Enabled != profile.IntegerScalingEnabled)
                currentGPU.SetIntegerScaling(profile.IntegerScalingEnabled, profile.IntegerScalingType);
        }

        private static void CurrentGPU_GPUScalingChanged(bool Supported, bool Enabled, int Mode)
        {
            if (!IsInitialized)
                return;

            // todo: use ProfileMager events
            Profile profile = ProfileManager.GetCurrent();

            if (Enabled != profile.GPUScaling)
                currentGPU.SetGPUScaling(profile.GPUScaling);
            if (Mode != profile.ScalingMode)
                currentGPU.SetScalingMode(profile.ScalingMode);
        }

        private static void CurrentGPU_ImageSharpeningChanged(bool Enabled, int Sharpness)
        {
            if (!IsInitialized)
                return;

            // todo: use ProfileMager events
            Profile profile = ProfileManager.GetCurrent();

            if (Enabled != profile.RISEnabled)
                currentGPU.SetImageSharpening(profile.RISEnabled);
            if (Sharpness != profile.RISSharpness)
                currentGPU.SetImageSharpeningSharpness(Sharpness);
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            lock (GPU.functionLock)
            {
                if (!IsLoaded_IGCL)
                    IsLoaded_IGCL = IGCLBackend.Initialize();

                if (!IsLoaded_ADLX)
                    IsLoaded_ADLX = ADLXBackend.IntializeAdlx();
            }

            // todo: check if usefull on resume
            // it could be DeviceManager_DisplayAdapterArrived is called already, making this redundant
            if (currentGPU is not null)
                currentGPU.Start();

            IsInitialized = true;
            Initialized?.Invoke(IsLoaded_IGCL, IsLoaded_ADLX);

            // manage events
            ProfileManager.Applied += ProfileManager_Applied;
            ProfileManager.Discarded += ProfileManager_Discarded;
            ProfileManager.Updated += ProfileManager_Updated;
            DeviceManager.DisplayAdapterArrived += DeviceManager_DisplayAdapterArrived;
            DeviceManager.DisplayAdapterRemoved += DeviceManager_DisplayAdapterRemoved;
            MultimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;

            LogManager.LogInformation("{0} has started", "GPUManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            // manage events
            ProfileManager.Applied -= ProfileManager_Applied;
            ProfileManager.Discarded -= ProfileManager_Discarded;
            ProfileManager.Updated -= ProfileManager_Updated;
            DeviceManager.DisplayAdapterArrived -= DeviceManager_DisplayAdapterArrived;
            DeviceManager.DisplayAdapterRemoved -= DeviceManager_DisplayAdapterRemoved;
            MultimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;

            foreach (GPU gpu in DisplayGPU.Values)
                gpu.Stop();

            lock (GPU.functionLock)
            {
                if (IsLoaded_IGCL)
                {
                    IGCLBackend.Terminate();
                    IsLoaded_IGCL = false;
                }

                if (IsLoaded_ADLX)
                {
                    ADLXBackend.CloseAdlx();
                    IsLoaded_ADLX = false;
                }
            }

            LogManager.LogInformation("{0} has stopped", "GPUManager");
        }

        private static void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            if (!IsInitialized || currentGPU is null)
                return;

            try
            {
                // apply profile GPU Scaling
                // apply profile scaling mode
                if (profile.GPUScaling)
                {
                    if (!currentGPU.GetGPUScaling())
                        currentGPU.SetGPUScaling(true);

                    if (currentGPU.GetScalingMode() != profile.ScalingMode)
                        currentGPU.SetScalingMode(profile.ScalingMode);
                }
                else if (currentGPU.GetGPUScaling())
                {
                    currentGPU.SetGPUScaling(false);
                }

                // apply profile RSR
                if (currentGPU is AMDGPU amdGPU)
                {
                    if (profile.RSREnabled)
                    {
                        if (!amdGPU.GetRSR())
                            amdGPU.SetRSR(true);

                        if (amdGPU.GetRSRSharpness() != profile.RSRSharpness)
                            amdGPU.SetRSRSharpness(profile.RSRSharpness);
                    }
                    else if (amdGPU.GetRSR())
                    {
                        amdGPU.SetRSR(false);
                    }
                }

                // apply profile Integer Scaling
                if (profile.IntegerScalingEnabled)
                {
                    if (!currentGPU.GetIntegerScaling())
                        currentGPU.SetIntegerScaling(true, profile.IntegerScalingType);
                }
                else if (currentGPU.GetIntegerScaling())
                {
                    currentGPU.SetIntegerScaling(false, 0);
                }

                // apply profile image sharpening
                if (profile.RISEnabled)
                {
                    if (!currentGPU.GetImageSharpening())
                        currentGPU.SetImageSharpening(profile.RISEnabled);

                    if (currentGPU.GetImageSharpeningSharpness() != profile.RISSharpness)
                        currentGPU.SetImageSharpeningSharpness(profile.RISSharpness);
                }
                else if (currentGPU.GetImageSharpening())
                {
                    currentGPU.SetImageSharpening(false);
                }
            }
            catch { }
        }

        private static void ProfileManager_Discarded(Profile profile)
        {
            if (!IsInitialized || currentGPU is null)
                return;

            try
            {
                /*
                // restore default GPU Scaling
                if (profile.GPUScaling && currentGPU.GetGPUScaling())
                    currentGPU.SetGPUScaling(false);
                */

                // restore default RSR
                if (currentGPU is AMDGPU amdGPU)
                {
                    if (profile.RSREnabled && amdGPU.GetRSR())
                        amdGPU.SetRSR(false);
                }

                // restore default integer scaling
                if (profile.IntegerScalingEnabled && currentGPU.GetIntegerScaling())
                    currentGPU.SetIntegerScaling(false, 0);

                // restore default image sharpening
                if (profile.RISEnabled && currentGPU.GetImageSharpening())
                    currentGPU.SetImageSharpening(false);
            }
            catch { }
        }

        // todo: moveme
        private static void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            ProcessEx.SetAppCompatFlag(profile.Path, ProcessEx.DisabledMaximizedWindowedValue, !profile.FullScreenOptimization);
            ProcessEx.SetAppCompatFlag(profile.Path, ProcessEx.HighDPIAwareValue, !profile.HighDPIAware);
        }
    }
}
