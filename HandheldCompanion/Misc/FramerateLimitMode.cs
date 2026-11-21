namespace HandheldCompanion.Misc
{
    /// <summary>
    /// How a settings layer defines the framerate limit.
    /// Inherit defers to the next layer: game profile, default game profile, power profile, then RTSS itself.
    /// Override applies the layer's own framerate value, where 0 means no limit.
    /// </summary>
    public enum FramerateLimitMode
    {
        Inherit = 0,
        // 1 was NoLimit in early builds, it is read as Override with a value of 0
        Override = 2,
    }

    public static class FramerateLimitModeExtensions
    {
        private const int LegacyNoLimit = 1;

        public static bool IsLegacyNoLimit(this FramerateLimitMode mode) => (int)mode == LegacyNoLimit;

        /// <summary>
        /// Maps any stored value onto Inherit or Override.
        /// </summary>
        public static FramerateLimitMode Normalize(this FramerateLimitMode mode) =>
            mode == FramerateLimitMode.Inherit ? FramerateLimitMode.Inherit : FramerateLimitMode.Override;
    }

    /// <summary>
    /// The layer a resolved framerate limit comes from.
    /// </summary>
    public enum FramerateLimitSource
    {
        GameProfile = 0,
        DefaultProfile = 1,
        PowerProfile = 2,
    }
}