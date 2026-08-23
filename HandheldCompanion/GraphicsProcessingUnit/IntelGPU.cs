using HandheldCompanion.IGCL;
using SharpDX.Direct3D9;
using System;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using static HandheldCompanion.IGCL.IGCLBackend;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    public class IntelGPU : GPU
    {
        #region events
        public event EnduranceGamingStateEventHandler? EnduranceGamingState;
        public delegate void EnduranceGamingStateEventHandler(bool Supported, ctl_3d_endurance_gaming_control_t Control, ctl_3d_endurance_gaming_mode_t Mode);
        #endregion

        // Intel® Graphics Software Service - static fields specific to IntelGPU
        public static string serviceName = "IntelGraphicsSoftwareService";
        private static ServiceController? serviceController = new ServiceController(serviceName);

        private bool prevEnduranceGamingSupport;
        private ctl_3d_endurance_gaming_control_t prevEGControl = new();
        private ctl_3d_endurance_gaming_mode_t prevEGMode = new();

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

        public override bool HasPrebuiltShaderDownload(out bool perAppSupported)
        {
            perAppSupported = false;
            if (!IsInitialized)
                return false;

            (bool supported, bool perApp) result = Execute(() =>
            {
                bool perAppResult;
                bool supportedResult = IGCLBackend.HasPrebuiltShaderDownload(deviceIdx, out perAppResult);
                return (supportedResult, perAppResult);
            }, (false, false));
            perAppSupported = result.perApp;
            return result.supported;
        }

        public override bool GetPrebuiltShaderDownload(string? applicationName, out bool enabled)
        {
            enabled = false;
            if (!IsInitialized || !HasPrebuiltShaderDownload(out bool perAppSupported) || (!string.IsNullOrEmpty(applicationName) && !perAppSupported))
                return false;

            bool result = false;
            bool success = Execute(() => IGCLBackend.GetPrebuiltShaderDownloadState(deviceIdx, applicationName, out result), false);
            enabled = result;
            return success;
        }

        public override bool SetPrebuiltShaderDownload(string? applicationName, bool enabled)
        {
            if (!IsInitialized || !HasPrebuiltShaderDownload(out bool perAppSupported) || (!string.IsNullOrEmpty(applicationName) && !perAppSupported))
                return false;

            return Execute(() => IGCLBackend.SetPrebuiltShaderDownloadState(deviceIdx, applicationName, enabled), false);
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

        public static bool HasServiceStatus(ServiceControllerStatus status)
        {
            try
            {
                return serviceController?.Status == status;
            }
            catch { }

            return false;
        }

        public IntelGPU(AdapterInformation adapterInformation) : base(adapterInformation)
        {
            deviceIdx = GetDeviceIdx(adapterInformation.Details.Description);
            if (deviceIdx == -1)
                return;

            IsInitialized = true;

            UpdateTimer = new Timer(UpdateInterval)
            {
                AutoReset = true
            };
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;

        }

        protected override void UpdateSettings()
        {
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

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (halting)
                return;

            UpdateSettings();
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
