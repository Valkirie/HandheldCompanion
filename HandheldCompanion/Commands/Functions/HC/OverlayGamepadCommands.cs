using HandheldCompanion.Views;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class OverlayGamepadCommands : FunctionCommands
    {
        public OverlayGamepadCommands()
        {
            base.Name = Properties.Resources.Hotkey_overlayGamepad;
            base.Description = Properties.Resources.Hotkey_overlayGamepadDesc;
            base.Glyph = "\ue7fc";
            base.OnKeyUp = true;

            MainWindow.overlayModel.IsVisibleChanged += IsVisibleChanged;
        }

        private void IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            base.Execute(OnKeyDown, OnKeyUp, true);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            MainWindow.overlayModel.ToggleVisibility();

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => MainWindow.overlayModel.Visibility == System.Windows.Visibility.Visible;

        public override object Clone()
        {
            OverlayGamepadCommands commands = new()
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
            MainWindow.overlayModel.IsVisibleChanged -= IsVisibleChanged;
            base.Dispose();
        }
    }
}
