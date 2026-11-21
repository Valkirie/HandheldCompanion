using HandheldCompanion.Managers.Desktop;
using System.Collections.Generic;
using Resources = HandheldCompanion.Properties.Resources;

namespace HandheldCompanion.ViewModels.Misc
{
    public class ScreenFramelimitViewModel
    {
        // a custom limit starts at 1, no limit (0) is picked through the Disabled preset
        public const int CustomMinimum = 1;

        /// <summary>
        /// Builds the framerate limit choices for a screen: its presets, Disabled (0) first, followed by Custom.
        /// </summary>
        public static List<ScreenFramelimitViewModel> BuildList(DesktopScreen? screen)
        {
            List<ScreenFramelimitViewModel> limits = [];

            if (screen is not null)
            {
                foreach (ScreenFramelimit frameLimit in screen.GetFramelimits())
                    limits.Add(new ScreenFramelimitViewModel(frameLimit));
            }

            limits.Add(new ScreenFramelimitViewModel(new ScreenFramelimit(-1, 0), Resources.Enum_InputsHotkeyType_Custom, true));
            return limits;
        }

        /// <summary>
        /// Builds the framerate limit choices for a refresh rate: its presets, Disabled (0) first, followed by Custom.
        /// A refresh rate of 0 or less (not known yet) lists Disabled and Custom, so no limit stays selectable.
        /// </summary>
        public static List<ScreenFramelimitViewModel> BuildList(int frequency)
        {
            List<ScreenFramelimitViewModel> limits = [];

            if (frequency > 0)
            {
                foreach (ScreenFramelimit frameLimit in DesktopScreen.GetFramelimits(frequency))
                    limits.Add(new ScreenFramelimitViewModel(frameLimit));
            }
            else
            {
                limits.Add(new ScreenFramelimitViewModel(new ScreenFramelimit(0, 0)));
            }

            limits.Add(new ScreenFramelimitViewModel(new ScreenFramelimit(-1, 0), Resources.Enum_InputsHotkeyType_Custom, true));
            return limits;
        }

        public ScreenFramelimit FrameLimit { get; }
        public bool IsCustom { get; }

        // value identity of an entry, stable when the list is rebuilt: the limit, -1 for Custom
        public const int CustomKey = -1;
        public int SelectionKey => IsCustom ? CustomKey : FrameLimit.limit;

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
