using HandheldCompanion.Views;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class MainWindowCommands : FunctionCommands
    {
        public MainWindowCommands()
        {
            base.Name = Properties.Resources.Hotkey_Mainwindow;
            base.Description = Properties.Resources.Hotkey_MainwindowDesc;
            base.Glyph = "\uE7C4";
            base.OnKeyUp = true;

            MainWindow.GetCurrent().StateChanged += StateChanged;
        }

        private void StateChanged(object? sender, EventArgs e)
        {
            base.Execute(OnKeyDown, OnKeyUp);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            MainWindow.GetCurrent().SwapWindowState();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override bool IsToggled => MainWindow.GetCurrent().WindowState != System.Windows.WindowState.Minimized;

        public override object Clone()
        {
            MainWindowCommands commands = new()
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
            MainWindow.GetCurrent().StateChanged -= StateChanged;
            base.Dispose();
        }
    }
}
