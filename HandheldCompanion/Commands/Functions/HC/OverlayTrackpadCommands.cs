using HandheldCompanion.Views;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class OverlayTrackpadCommands : FunctionCommands
    {
        public OverlayTrackpadCommands()
        {
            base.Name = Properties.Resources.Hotkey_overlayTrackpads;
            base.Description = Properties.Resources.Hotkey_overlayTrackpadsDesc;
            base.Glyph = "\uEDA4";
            base.OnKeyUp = true;

            MainWindow.overlayTrackpad.IsVisibleChanged += IsVisibleChanged;
        }

        private void IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            base.Execute(OnKeyDown, OnKeyUp);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            MainWindow.overlayTrackpad.ToggleVisibility();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override bool IsToggled => MainWindow.overlayTrackpad.Visibility == System.Windows.Visibility.Visible;

        public override object Clone()
        {
            OverlayTrackpadCommands commands = new()
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
            MainWindow.overlayTrackpad.IsVisibleChanged -= IsVisibleChanged;
            base.Dispose();
        }
    }
}
