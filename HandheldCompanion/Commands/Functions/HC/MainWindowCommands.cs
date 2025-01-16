using HandheldCompanion.Helpers;
using HandheldCompanion.Views;
using System;

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
                2 => "DevicePage",
                3 => "PerformancePage",
                4 => "ProfilesPage",
                5 => "OverlayPage",
                6 => "HotkeysPage",
                7 => "AboutPage",
                8 => "NotificationsPage",
                9 => "SettingsPage",
                _ => string.Empty
            };

            var mainWindow = MainWindow.GetCurrent();

            // Toggle visibility if no page change or the page being navigated to is different from the current one
            if (string.IsNullOrEmpty(pageTag) || pageTag == mainWindow.prevNavItemTag)
                mainWindow.SwapWindowState();

            // Navigate to the specified page if valid
            if (!string.IsNullOrEmpty(pageTag))
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    mainWindow.NavigateToPage(pageTag);
                });
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
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
