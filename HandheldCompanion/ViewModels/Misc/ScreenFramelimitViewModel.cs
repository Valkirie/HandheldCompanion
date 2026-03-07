using HandheldCompanion.Managers.Desktop;

namespace HandheldCompanion.ViewModels.Misc
{
    public class ScreenFramelimitViewModel
    {
        public ScreenFramelimit FrameLimit { get; }

        public ScreenFramelimitViewModel(ScreenFramelimit frameLimit)
        {
            FrameLimit = frameLimit;
        }

        public override string ToString()
        {
            return FrameLimit.ToString();
        }
    }
}
