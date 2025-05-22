using HandheldCompanion.Helpers;
using HandheldCompanion.Views.Windows;
using System;
using System.Windows;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class QuickToolsCommands : FunctionCommands
    {
        public int PageIndex { get; set; } = 0;

        public QuickToolsCommands()
        {
            base.Name = Properties.Resources.Hotkey_quickTools;
            base.Description = Properties.Resources.Hotkey_quickToolsDesc;
            base.Glyph = "\uEC7A";
            base.OnKeyUp = true;

            OverlayQuickTools.GetCurrent().IsVisibleChanged += IsVisibleChanged;
        }

        private void IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            base.Execute(OnKeyDown, OnKeyUp, true);
        }

        public override void Execute(bool isKeyDown, bool isKeyUp, bool isBackground)
        {
            string pageTag = PageIndex switch
            {
                // 0 => "Current",
                1 => "QuickHomePage",
                2 => "QuickDevicePage",
                3 => "QuickProfilesPage",
                4 => "QuickApplicationsPage",
                _ => string.Empty
            };

            OverlayQuickTools overlayQuickTools = OverlayQuickTools.GetCurrent();

            // Toggle visibility
            overlayQuickTools.ToggleVisibility();

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                switch (overlayQuickTools.Visibility)
                {
                    case Visibility.Visible:
                        // Navigate to the specified page if valid
                        if (!string.IsNullOrEmpty(pageTag))
                            overlayQuickTools.NavigateToPage(pageTag);
                        break;
                }
            });

            base.Execute(isKeyDown, isKeyUp, false);
        }

        public override bool IsToggled => OverlayQuickTools.GetCurrent().Visibility == Visibility.Visible;

        public override object Clone()
        {
            QuickToolsCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown
            };

            return commands;
        }

        public override void Dispose()
        {
            OverlayQuickTools.GetCurrent().IsVisibleChanged -= IsVisibleChanged;
            base.Dispose();
        }
    }
}
