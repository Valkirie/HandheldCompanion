using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// How a settings layer defines a display mode field.
    /// Inherit defers to the next layer: game profile, default game profile, then no change.
    /// Override applies the layer's own value.
    /// </summary>
    public enum DisplayOverrideMode
    {
        Inherit = 0,
        Override = 1,
    }

    /// <summary>
    /// The layer a resolved display mode field comes from.
    /// </summary>
    public enum DisplayModeSource
    {
        None = 0,
        GameProfile = 1,
        DefaultProfile = 2,
    }

    /// <summary>
    /// Effective display mode of one monitor. A field whose source is None is left unchanged.
    /// </summary>
    public readonly record struct ResolvedDisplayMode(
        int Width,
        int Height,
        DisplayModeSource ResolutionSource,
        int RefreshRate,
        DisplayModeSource RefreshSource)
    {
        public bool HasResolution => ResolutionSource != DisplayModeSource.None;
        public bool HasRefreshRate => RefreshSource != DisplayModeSource.None;
    }

    /// <summary>
    /// Resolution and refresh rate overrides for one monitor, stored per power state in a game profile.
    /// Resolution and refresh rate are resolved independently.
    /// </summary>
    [Serializable]
    public class DisplayModeOverride
    {
        public DisplayOverrideMode ResolutionMode { get; set; } = DisplayOverrideMode.Inherit;
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;

        public DisplayOverrideMode RefreshMode { get; set; } = DisplayOverrideMode.Inherit;
        public int RefreshRate { get; set; } = 0;

        [JsonIgnore]
        public bool HasResolution => ResolutionMode == DisplayOverrideMode.Override && Width > 0 && Height > 0;

        [JsonIgnore]
        public bool HasRefreshRate => RefreshMode == DisplayOverrideMode.Override && RefreshRate > 0;
    }
}
