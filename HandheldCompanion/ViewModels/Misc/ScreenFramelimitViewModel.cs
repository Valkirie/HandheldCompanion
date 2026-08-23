using HandheldCompanion.Managers.Desktop;

namespace HandheldCompanion.ViewModels.Misc
{
    public class ScreenFramelimitViewModel
    {
        public ScreenFramelimit FrameLimit { get; }
        public bool IsCustom { get; }

        private readonly string? _displayName;

        public ScreenFramelimitViewModel(ScreenFramelimit frameLimit)
        {
            FrameLimit = frameLimit;
        }

        public ScreenFramelimitViewModel(ScreenFramelimit frameLimit, string displayName, bool isCustom)
        {
            FrameLimit = frameLimit;
            _displayName = displayName;
            IsCustom = isCustom;
        }

        public override string ToString()
        {
            if (!IsCustom && FrameLimit.limit == 0)
                return "Disable";

            return _displayName ?? FrameLimit.ToString();
        }
    }
}
