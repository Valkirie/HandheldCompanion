using HandheldCompanion.Managers.Desktop;

namespace HandheldCompanion.ViewModels.Misc
{
    public class ScreenDividerViewModel
    {
        public ScreenDivider ScreenDivider { get; }

        public ScreenDividerViewModel(ScreenDivider screenDivider)
        {
            ScreenDivider = screenDivider;
        }

        public int Divider => ScreenDivider.divider;

        public override string ToString()
        {
            return ScreenDivider.ToString();
        }
    }
}
