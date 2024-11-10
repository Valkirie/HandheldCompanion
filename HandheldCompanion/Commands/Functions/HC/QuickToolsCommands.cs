using HandheldCompanion.Views.Windows;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class QuickToolsCommands : FunctionCommands
    {
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

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            OverlayQuickTools.GetCurrent().ToggleVisibility();

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => OverlayQuickTools.GetCurrent().Visibility == System.Windows.Visibility.Visible;

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
