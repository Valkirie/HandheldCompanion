using SharpDX.Direct3D9;

namespace HandheldCompanion.GraphicsProcessingUnit
{
    /// <summary>
    /// Placeholder implementation for Unknown GPUs.
    /// This class inherits from the base GPU class but does not implement specific
    /// return default "unsupported" or "disabled" values.
    /// </summary>
    public class UnknownGPU : GPU
    {
        public UnknownGPU(AdapterInformation adapterInformation) : base(adapterInformation)
        {
            IsInitialized = true;
        }

        #region Feature Support (Returns false)

        public override bool HasGPUScalingSupport() => false;
        public override bool HasIntegerScalingSupport() => false;
        public override bool HasScalingModeSupport() => false;


        #endregion

        #region Feature Setters (Returns false)

        public override bool SetGPUScaling(bool enabled) => false;
        public override bool SetIntegerScaling(bool enabled, byte type) => false;
        public override bool SetScalingMode(int scalingMode) => false;
        public override bool SetImageSharpening(bool enabled) => false;
        public override bool SetImageSharpeningSharpness(int sharpness) => false;

        #endregion

        #region Feature Getters (Returns default/disabled values)

        public override bool GetGPUScaling() => false;
        public override bool GetIntegerScaling() => false;
        public override int GetScalingMode() => 0;
        public override bool GetImageSharpening() => false;
        public override int GetImageSharpeningSharpness() => 0;

        #endregion

        #region Telemetry (Returns default/zero values)

        public override bool HasClock() => false;
        public override float GetClock() => 0.0f;
        public override bool HasLoad() => false;
        public override float GetLoad() => 0.0f;
        public override bool HasPower() => false;
        public override float GetPower() => 0.0f;
        public override bool HasTemperature() => false;
        public override float GetTemperature() => 0.0f;
        public override float GetVRAMUsage() => 0.0f;

        #endregion
    }
}