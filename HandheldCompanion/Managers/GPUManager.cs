using HandheldCompanion.ADLX;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.IGCL;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Watchers;
using SharpDX.Direct3D9;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using static HandheldCompanion.IGCL.IGCLBackend;

namespace HandheldCompanion.Managers
{
    public class GPUManager : IManager
    {
        #region events
        public event HookedEventHandler? Hooked;
        public delegate void HookedEventHandler(GPU GPU);

        public event UnhookedEventHandler? Unhooked;
        public delegate void UnhookedEventHandler(GPU GPU);
        #endregion

        public static bool IsLoaded_IGCL = false;
        public static bool IsLoaded_ADLX = false;

        private static GPU currentGPU = null;
        private static ConcurrentDictionary<AdapterInformation, GPU> DisplayGPU = new();

        // watcher(s)
        private AMDSettingsWatcher AMDSettingsWatcher = new();

        private object screenLock = new();

        public override void Start()
        {
            if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
                return;

            base.PrepareStart();

            if (!IsLoaded_IGCL && GPU.HasIntelGPU())
            {
                // wait until Intel GPU service is ready
                DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(7));
                while (DateTime.Now < timeout && !IntelGPU.HasServiceStatus(ServiceControllerStatus.Running))
                    Thread.Sleep(1000);

                if (!IntelGPU.HasServiceStatus(ServiceControllerStatus.Running))
                    LogManager.LogError("{0} is not ready. Some GPU related features might not work as expected", IntelGPU.serviceName);

                // try to initialized IGCL
                IsLoaded_IGCL = IGCLBackend.Initialize();

                if (IsLoaded_IGCL)
                    LogManager.LogInformation("{0} was successfully initialized", "IGCL");
                else
                    LogManager.LogError("Failed to initialize {0}", "IGCL");
            }

            if (!IsLoaded_ADLX && GPU.HasAMDGPU())
            {
                // try to initialized ADLX
                IsLoaded_ADLX = ADLXBackend.SafeIntializeAdlx();

                if (IsLoaded_ADLX)
                    LogManager.LogInformation("{0} {1} was successfully initialized", "ADLX", ADLXBackend.GetVersion());
                else
                    LogManager.LogError("Failed to initialize {0}", "ADLX");
            }

            AMDSettingsWatcher.Start();

            // todo: check if usefull on resume
            // it could be DeviceManager_DisplayAdapterArrived is called already, making this redundant
            currentGPU?.Start();

            // manage events
            ManagerFactory.deviceManager.DisplayAdapterArrived += DeviceManager_DisplayAdapterArrived;
            ManagerFactory.deviceManager.DisplayAdapterRemoved += DeviceManager_DisplayAdapterRemoved;
            ManagerFactory.multimediaManager.PrimaryScreenChanged += MultimediaManager_PrimaryScreenChanged;

            // raise events
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }
            switch (ManagerFactory.powerProfileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryPowerProfile();
                    break;
            }

            switch (ManagerFactory.deviceManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.deviceManager.Initialized += DeviceManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryDevices();
                    break;
            }

            base.Start();
        }

        public override void Stop()
        {
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            base.PrepareStop();

            // manage events
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
            ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
            ManagerFactory.powerProfileManager.Discarded -= PowerProfileManager_Discarded;
            ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
            ManagerFactory.deviceManager.DisplayAdapterArrived -= DeviceManager_DisplayAdapterArrived;
            ManagerFactory.deviceManager.DisplayAdapterRemoved -= DeviceManager_DisplayAdapterRemoved;
            ManagerFactory.deviceManager.Initialized -= DeviceManager_Initialized;
            ManagerFactory.multimediaManager.PrimaryScreenChanged -= MultimediaManager_PrimaryScreenChanged;

            foreach (GPU gpu in DisplayGPU.Values)
                gpu.Stop();

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

            AMDSettingsWatcher.Stop();

            base.Stop();
        }

        private void QueryProfile()
        {
            // manage events
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded += ProfileManager_Discarded;
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;

            ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void QueryPowerProfile()
        {
            // manage events
            ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;
            ManagerFactory.powerProfileManager.Discarded += PowerProfileManager_Discarded;

            PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
        }

        private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
        {
            if (!IsReady || currentGPU is null)
                return;

            if (currentGPU is IntelGPU intelGPU)
            {
                intelGPU.SetEnduranceGaming(profile.IntelEnduranceGamingEnabled ? ctl_3d_endurance_gaming_control_t.AUTO : ctl_3d_endurance_gaming_control_t.OFF, (ctl_3d_endurance_gaming_mode_t)profile.IntelEnduranceGamingPreset);
            }
        }

        private void PowerProfileManager_Discarded(PowerProfile profile, bool swapped)
        {
            if (!IsReady || currentGPU is null)
                return;

            // don't bother discarding settings, new one will be enforce shortly
            if (swapped)
                return;

            if (currentGPU is IntelGPU intelGPU)
            {
                intelGPU.SetEnduranceGaming(ctl_3d_endurance_gaming_control_t.OFF, ctl_3d_endurance_gaming_mode_t.PERFORMANCE);
            }
        }

        private void PowerProfileManager_Initialized()
        {
            QueryPowerProfile();
        }

        private void DeviceManager_Initialized()
        {
            QueryDevices();
        }

        private void QueryDevices()
        {
            // use ConcurrentDictionary's thread-safe operations to avoid collection errors
            foreach (KeyValuePair<Guid, AdapterInformation> kvp in ManagerFactory.deviceManager.displayAdapters)
                DeviceManager_DisplayAdapterArrived(kvp.Value);
        }

        private void GPUConnect(GPU GPU)
        {
            LogManager.LogInformation("Connecting DisplayAdapter {0}", GPU.ToString());

            // update current GPU
            currentGPU = GPU;

            GPU.ImageSharpeningChanged += CurrentGPU_ImageSharpeningChanged;
            GPU.GPUScalingChanged += CurrentGPU_GPUScalingChanged;
            GPU.IntegerScalingChanged += CurrentGPU_IntegerScalingChanged;

            if (GPU is AMDGPU amdGPU)
            {
                amdGPU.RSRStateChanged += CurrentGPU_RSRStateChanged;
                amdGPU.AFMFStateChanged += CurrentGPU_AFMFStateChanged;
            }
            else if (GPU is IntelGPU intelGPU)
            {
                intelGPU.EnduranceGamingState += IntelGPU_EnduranceGamingState;
            }

            if (GPU.IsInitialized)
            {
                GPU.Start();
                Hooked?.Invoke(GPU);

                LogManager.LogInformation("Hooked DisplayAdapter: {0}", GPU.ToString());
            }
        }

        private void GPUDisconnect(GPU GPU)
        {
            LogManager.LogInformation("Disconnecting DisplayAdapter {0}", GPU.ToString());

            if (currentGPU == GPU)
                Unhooked?.Invoke(GPU);

            GPU.ImageSharpeningChanged -= CurrentGPU_ImageSharpeningChanged;
            GPU.GPUScalingChanged -= CurrentGPU_GPUScalingChanged;
            GPU.IntegerScalingChanged -= CurrentGPU_IntegerScalingChanged;

            if (GPU is AMDGPU amdGPU)
            {
                amdGPU.RSRStateChanged -= CurrentGPU_RSRStateChanged;
                amdGPU.AFMFStateChanged -= CurrentGPU_AFMFStateChanged;
            }
            else if (GPU is IntelGPU intelGPU)
            {
                intelGPU.EnduranceGamingState -= IntelGPU_EnduranceGamingState;
            }

            GPU.Stop();
        }

        private void DeviceManager_DisplayAdapterArrived(AdapterInformation adapterInformation)
        {
            // GPU is already part of the dictionary
            if (DisplayGPU.ContainsKey(adapterInformation))
                return;

            GPU? newGPU = null;

            if ((adapterInformation.Details.Description.Contains("Advanced Micro Devices") || adapterInformation.Details.Description.Contains("AMD")) && IsLoaded_ADLX)
            {
                newGPU = new AMDGPU(adapterInformation);
            }
            else if (adapterInformation.Details.Description.Contains("Intel") && IsLoaded_IGCL)
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

            LogManager.LogInformation("Detected DisplayAdapter: {0}, VendorID:{1}, DeviceId:{2}", adapterInformation.Details.Description, adapterInformation.Details.VendorId, adapterInformation.Details.DeviceId);

            // Add to dictionary
            DisplayGPU.TryAdd(adapterInformation, newGPU);

            // Wait until manager is ready
            if (ManagerFactory.multimediaManager.IsRunning)
            {
                while (ManagerFactory.multimediaManager.IsBusy)
                    Thread.Sleep(1000);

                // Force send an update
                if (ManagerFactory.multimediaManager.PrimaryDesktop != null)
                    MultimediaManager_PrimaryScreenChanged(ManagerFactory.multimediaManager.PrimaryDesktop);
            }
        }

        private void MultimediaManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            lock (screenLock)
            {
                try
                {
                    AdapterInformation? key = DisplayGPU.Keys.FirstOrDefault(GPU => GPU.Details.DeviceName == screen.screen.DeviceName);
                    if (key is not null && DisplayGPU.TryGetValue(key, out GPU? gpu))
                    {
                        LogManager.LogInformation("Retrieved DisplayAdapter: {0} for screen: {1}", gpu.ToString(), screen.ToString());

                        if (currentGPU != gpu)
                        {
                            // Disconnect from the current GPU, if any
                            if (currentGPU is not null)
                                GPUDisconnect(currentGPU);

                            // Connect to the new GPU
                            GPUConnect(gpu);
                        }
                    }
                    else
                    {
                        LogManager.LogError("Failed to retrieve DisplayAdapter for screen: {0}", screen.ToString());
                    }
                }
                catch
                {
                    LogManager.LogError("Failed to retrieve DisplayAdapter for screen: {0}, {1}", screen.ToString());
                }
            }
        }

        private void DeviceManager_DisplayAdapterRemoved(AdapterInformation adapterInformation)
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

        private void CurrentGPU_RSRStateChanged(bool Supported, bool Enabled, int Sharpness)
        {
            if (!IsReady)
                return;

            // todo: use ProfileManager events
            Profile profile = ManagerFactory.profileManager.GetCurrent();
            AMDGPU amdGPU = (AMDGPU)currentGPU;

            if (Enabled != profile.RSREnabled)
                amdGPU.SetRSR(profile.RSREnabled);
            if (Sharpness != profile.RSRSharpness)
                amdGPU.SetRSRSharpness(profile.RSRSharpness);
        }

        private void CurrentGPU_AFMFStateChanged(bool Supported, bool Enabled)
        {
            if (!IsReady)
                return;

            // todo: use ProfileManager events
            Profile profile = ManagerFactory.profileManager.GetCurrent();
            AMDGPU amdGPU = (AMDGPU)currentGPU;

            if (Enabled != profile.AFMFEnabled)
                amdGPU.SetAFMF(profile.AFMFEnabled);
        }

        private void CurrentGPU_IntegerScalingChanged(bool Supported, bool Enabled)
        {
            if (!IsReady)
                return;

            // todo: use ProfileManager events
            Profile profile = ManagerFactory.profileManager.GetCurrent();

            if (Enabled != profile.IntegerScalingEnabled)
                currentGPU.SetIntegerScaling(profile.IntegerScalingEnabled, profile.IntegerScalingType);
        }

        private void CurrentGPU_GPUScalingChanged(bool Supported, bool Enabled, int Mode)
        {
            if (!IsReady)
                return;

            // todo: use ProfileManager events
            Profile profile = ManagerFactory.profileManager.GetCurrent();

            if (Enabled != profile.GPUScaling)
                currentGPU.SetGPUScaling(profile.GPUScaling);
            if (Mode != profile.ScalingMode)
                currentGPU.SetScalingMode(profile.ScalingMode);
        }

        private void IntelGPU_EnduranceGamingState(bool Supported, ctl_3d_endurance_gaming_control_t Control, ctl_3d_endurance_gaming_mode_t Mode)
        {
            if (!IsReady)
                return;

            // todo: use PowerProfileManager events
            PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetCurrent();
            IntelGPU intelGPU = (IntelGPU)currentGPU;

            bool IntelEnduranceGamingEnabled = Control == ctl_3d_endurance_gaming_control_t.ON || Control == ctl_3d_endurance_gaming_control_t.AUTO;
            if (IntelEnduranceGamingEnabled != powerProfile.IntelEnduranceGamingEnabled)
                intelGPU.SetEnduranceGaming(powerProfile.IntelEnduranceGamingEnabled ? ctl_3d_endurance_gaming_control_t.AUTO : ctl_3d_endurance_gaming_control_t.OFF, (ctl_3d_endurance_gaming_mode_t)powerProfile.IntelEnduranceGamingPreset);
        }

        private void CurrentGPU_ImageSharpeningChanged(bool Enabled, int Sharpness)
        {
            if (!IsReady)
                return;

            // todo: use ProfileManager events
            Profile profile = ManagerFactory.profileManager.GetCurrent();

            if (Enabled != profile.RISEnabled)
                currentGPU.SetImageSharpening(profile.RISEnabled);
            if (Sharpness != profile.RISSharpness)
                currentGPU.SetImageSharpeningSharpness(Sharpness);
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            if (!IsReady || currentGPU is null)
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

                // apply profile RSR / AFMF
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

                    if (profile.AFMFEnabled)
                    {
                        if (!amdGPU.GetAFMF())
                            amdGPU.SetAFMF(true);

                        if (!amdGPU.GetAntiLag())
                            amdGPU.SetAntiLag(true);
                    }
                    else if (amdGPU.GetAFMF())
                    {
                        amdGPU.SetAFMF(false);
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

        private void ProfileManager_Discarded(Profile profile, bool swapped, Profile nextProfile)
        {
            if (!IsReady || currentGPU is null)
                return;

            // don't bother discarding settings, new one will be enforce shortly
            if (swapped)
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
        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            ProcessEx.SetAppCompatFlag(profile.Path, ProcessEx.DisabledMaximizedWindowedValue, !profile.FullScreenOptimization);
            ProcessEx.SetAppCompatFlag(profile.Path, ProcessEx.HighDPIAwareValue, !profile.HighDPIAware);
        }
    }
}
