using Newtonsoft.Json;
using System;
using System.Runtime.Serialization;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// How a settings layer defines a graphics group.
    /// Inherit defers to the next layer: game profile, then default game profile, which always overrides every group.
    /// Override applies the layer's own values, including off.
    /// </summary>
    public enum GraphicsOverrideMode
    {
        Inherit = 0,
        Override = 1,
    }

    /// <summary>
    /// The layer a resolved graphics group comes from. None leaves the driver value alone.
    /// </summary>
    public enum GraphicsSource
    {
        None = 0,
        GameProfile = 1,
        DefaultProfile = 2,
    }

    /// <summary>
    /// One graphics group: a mode and the values it applies when overridden.
    /// </summary>
    [Serializable]
    public abstract class GraphicsGroupOverride
    {
        public GraphicsOverrideMode Mode { get; set; } = GraphicsOverrideMode.Inherit;
        public bool Enabled { get; set; }

        [JsonIgnore]
        public bool IsOverride => Mode == GraphicsOverrideMode.Override;

        /// <summary>
        /// Copies the values of the same group, not its mode.
        /// </summary>
        public void CopyValuesFrom(GraphicsGroupOverride source)
        {
            if (source.GetType() != GetType())
                return;

            Enabled = source.Enabled;
            CopyGroupValues(source);
        }

        protected abstract void CopyGroupValues(GraphicsGroupOverride source);
    }

    /// <summary>
    /// GPU scaling and its scaling mode.
    /// </summary>
    [Serializable]
    public class ScalingOverride : GraphicsGroupOverride
    {
        public int ScalingMode { get; set; } = 0; // default AMD value

        protected override void CopyGroupValues(GraphicsGroupOverride source)
        {
            ScalingMode = ((ScalingOverride)source).ScalingMode;
        }
    }

    /// <summary>
    /// AMD Radeon Super Resolution.
    /// </summary>
    [Serializable]
    public class RSROverride : GraphicsGroupOverride
    {
        public int Sharpness { get; set; } = 20; // default AMD value

        protected override void CopyGroupValues(GraphicsGroupOverride source)
        {
            Sharpness = ((RSROverride)source).Sharpness;
        }
    }

    /// <summary>
    /// Integer scaling, its resolution divider and scaling type.
    /// </summary>
    [Serializable]
    public class IntegerScalingOverride : GraphicsGroupOverride
    {
        public int Divider { get; set; } = 1;
        public byte Type { get; set; } = 0;

        protected override void CopyGroupValues(GraphicsGroupOverride source)
        {
            IntegerScalingOverride integerScaling = (IntegerScalingOverride)source;
            Divider = integerScaling.Divider;
            Type = integerScaling.Type;
        }
    }

    /// <summary>
    /// Image sharpening (RIS on AMD).
    /// </summary>
    [Serializable]
    public class RISOverride : GraphicsGroupOverride
    {
        public int Sharpness { get; set; } = 80; // default AMD value

        protected override void CopyGroupValues(GraphicsGroupOverride source)
        {
            Sharpness = ((RISOverride)source).Sharpness;
        }
    }

    /// <summary>
    /// AMD Fluid Motion Frames and its 2.1 sub-controls.
    /// </summary>
    [Serializable]
    public class AFMFOverride : GraphicsGroupOverride
    {
        public int Algorithm { get; set; } = 0;          // AFMF_ALGORITHM_AUTO
        public int SearchMode { get; set; } = 0;         // AFMF_SEARCH_MODE_AUTO
        public int PerformanceMode { get; set; } = 0;    // AFMF_PERFORMANCE_MODE_AUTO
        public int FastMotionResponse { get; set; } = 0; // AFMF_RESP_REPEAT_FRAMES

        protected override void CopyGroupValues(GraphicsGroupOverride source)
        {
            AFMFOverride afmf = (AFMFOverride)source;
            Algorithm = afmf.Algorithm;
            SearchMode = afmf.SearchMode;
            PerformanceMode = afmf.PerformanceMode;
            FastMotionResponse = afmf.FastMotionResponse;
        }
    }

    /// <summary>
    /// Graphics groups of one power state, stored in a game profile.
    /// Each group is resolved on its own: game profile, then default game profile.
    /// </summary>
    [Serializable]
    public class GraphicsOverrides
    {
        public ScalingOverride Scaling { get; set; } = new();
        public RSROverride RSR { get; set; } = new();
        public IntegerScalingOverride IntegerScaling { get; set; } = new();
        public RISOverride RIS { get; set; } = new();
        public AFMFOverride AFMF { get; set; } = new();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            Scaling ??= new();
            RSR ??= new();
            IntegerScaling ??= new();
            RIS ??= new();
            AFMF ??= new();
        }
    }

    /// <summary>
    /// Effective values of one graphics group. Values is null when the source is None.
    /// </summary>
    public readonly record struct ResolvedGraphicsGroup<T>(T? Values, GraphicsSource Source) where T : GraphicsGroupOverride
    {
        public bool IsSet => Source != GraphicsSource.None && Values is not null;

        // an unset group reads as off, it only matters for the groups that depend on it
        public bool IsEnabled => IsSet && Values!.Enabled;
    }

    /// <summary>
    /// Effective graphics groups of a game profile on one power state.
    /// </summary>
    public readonly record struct ResolvedGraphics(
        ResolvedGraphicsGroup<ScalingOverride> Scaling,
        ResolvedGraphicsGroup<RSROverride> RSR,
        ResolvedGraphicsGroup<IntegerScalingOverride> IntegerScaling,
        ResolvedGraphicsGroup<RISOverride> RIS,
        ResolvedGraphicsGroup<AFMFOverride> AFMF);
}
