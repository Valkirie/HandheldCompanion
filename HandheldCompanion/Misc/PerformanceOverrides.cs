using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// Per power-state overrides stored in a game profile.
    /// Inherit means the setting is resolved from the default game profile,
    /// then from the power profile selected for the same power state.
    /// Override applies FramerateValue, where 0 means no limit.
    /// DisplayModes holds resolution and refresh rate overrides per known monitor (key: KnownMonitor.Id).
    /// A monitor without an entry inherits; display modes have no power profile layer.
    /// Graphics holds the graphics groups, each resolved on its own; graphics have no power profile layer either.
    /// </summary>
    [Serializable]
    public class PerformanceOverrides
    {
        public FramerateLimitMode FramerateMode { get; set; } = FramerateLimitMode.Inherit;
        public int FramerateValue { get; set; } = 0;

        public Dictionary<Guid, DisplayModeOverride> DisplayModes { get; set; } = new();

        public GraphicsOverrides Graphics { get; set; } = new();

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            // early builds stored NoLimit as its own mode
            if (FramerateMode.IsLegacyNoLimit())
                FramerateValue = 0;

            FramerateMode = FramerateMode.Normalize();
            DisplayModes ??= new();
            Graphics ??= new();
        }
    }
}