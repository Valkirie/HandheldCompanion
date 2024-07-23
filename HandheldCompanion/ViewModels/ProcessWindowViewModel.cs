using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class ProcessWindowViewModel : BaseViewModel
    {
        private ProcessExViewModel ProcessExViewModel { get; set; }

        public bool HasTwoScreen => Screen.AllScreens.Length > 1;
        private Screen CurrentScreen = Screen.PrimaryScreen;
        public bool IsPrimaryScreen => CurrentScreen.Primary;

        public ICommand BringProcessCommand { get; private set; }
        public ICommand SwapScreenCommand { get; private set; }

        public string Name => ProcessWindow.Name;

        private ProcessWindow _ProcessWindow;
        public ProcessWindow ProcessWindow
        {
            get => _ProcessWindow;
            set
            {
                _ProcessWindow = value;
                OnPropertyChanged(nameof(ProcessWindow));
            }
        }

        public ProcessWindowViewModel(ProcessWindow processWindow, ProcessExViewModel processExViewModel)
        {
            ProcessWindow = processWindow;
            ProcessWindow.Refreshed += ProcessRefreshed;
            ProcessExViewModel = processExViewModel;

            MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

            BringProcessCommand = new DelegateCommand(async () =>
            {
                OverlayQuickTools qtWindow = OverlayQuickTools.GetCurrent();

                ContentDialogResult result = await qtWindow.applicationsPage.SnapDialog.ShowAsync(qtWindow);
                switch (result)
                {
                    case ContentDialogResult.None:
                        qtWindow.applicationsPage.SnapDialog.Hide();
                        return;
                }

                // Get the screen where the reference window is located
                Screen screen = Screen.FromHandle(ProcessWindow.Hwnd);
                if (screen is null)
                    return;

                WinAPI.MakeBorderless(ProcessWindow.Hwnd, processExViewModel.PageViewModel.BorderlessEnabled && processExViewModel.PageViewModel.BorderlessToggle);
                WinAPI.MoveWindow(ProcessWindow.Hwnd, screen, processExViewModel.PageViewModel.windowPositions);
                WinAPI.SetForegroundWindow(ProcessWindow.Hwnd);
            });

            SwapScreenCommand = new DelegateCommand(async () =>
            {
                Screen screen = Screen.AllScreens.Where(screen => screen.DeviceName != CurrentScreen.DeviceName).FirstOrDefault();
                WinAPI.MoveWindow(ProcessWindow.Hwnd, screen, WpfScreenHelper.Enum.WindowPositions.Maximize);
                WinAPI.SetForegroundWindow(ProcessWindow.Hwnd);

                OnPropertyChanged(nameof(IsPrimaryScreen));
            });
        }

        private void MultimediaManager_DisplaySettingsChanged(Managers.Desktop.DesktopScreen screen, Managers.Desktop.ScreenResolution resolution)
        {
            // update current screen
            CurrentScreen = Screen.FromHandle(ProcessWindow.Hwnd);

            OnPropertyChanged(nameof(IsPrimaryScreen));
            OnPropertyChanged(nameof(HasTwoScreen));
        }

        private void ProcessRefreshed(object? sender, EventArgs e)
        {
            // update current screen
            CurrentScreen = Screen.FromHandle(ProcessWindow.Hwnd);

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPrimaryScreen));
        }

        public override void Dispose()
        {
            if (ProcessWindow is not null)
                ProcessWindow.Refreshed -= ProcessRefreshed;

            base.Dispose();
        }
    }
}
