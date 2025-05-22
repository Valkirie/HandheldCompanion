using HandheldCompanion.Helpers;
using HandheldCompanion.Views;
using System;
using System.Windows;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class MainWindowCommands : FunctionCommands
    {
        public int PageIndex { get; set; } = 0;

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
            base.Execute(OnKeyDown, OnKeyUp, true);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            string pageTag = PageIndex switch
            {
                // 0 => "Current",
                1 => "ControllerPage",
                2 => "LibraryPage",
                3 => "DevicePage",
                4 => "PerformancePage",
                5 => "ProfilesPage",
                6 => "OverlayPage",
                7 => "HotkeysPage",
                8 => "AboutPage",
                9 => "NotificationsPage",
                10 => "SettingsPage",
                _ => string.Empty
            };

            MainWindow mainWindow = MainWindow.GetCurrent();

            // Toggle state
            mainWindow.SwapWindowState();

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                switch (mainWindow.Visibility)
                {
                    case Visibility.Visible:
                        // Navigate to the specified page if valid
                        if (!string.IsNullOrEmpty(pageTag))
                            mainWindow.NavigateToPage(pageTag);
                        break;
                }
            });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => MainWindow.GetCurrent().WindowState != WindowState.Minimized;

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
