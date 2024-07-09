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
            base.Execute(OnKeyDown, OnKeyUp);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            MainWindow.overlayModel.ToggleVisibility();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override bool IsToggled => MainWindow.overlayModel.Visibility == System.Windows.Visibility.Visible;

        public override object Clone()
        {
            OverlayGamepadCommands commands = new();
            commands.commandType = this.commandType;
            commands.Name = this.Name;
            commands.Description = this.Description;
            commands.Glyph = this.Glyph;
            commands.OnKeyUp = this.OnKeyUp;
            commands.OnKeyDown = this.OnKeyDown;

            return commands;
        }

        public override void Dispose()
        {
            MainWindow.overlayModel.IsVisibleChanged -= IsVisibleChanged;
            base.Dispose();
        }
    }
}
