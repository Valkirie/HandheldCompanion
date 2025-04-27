using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
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
        public bool HasTwoScreen => Screen.AllScreens.Length > 1;
        private Screen CurrentScreen = Screen.PrimaryScreen;
        public bool IsPrimaryScreen => CurrentScreen.Primary;

        public ICommand BringProcessCommand { get; private set; }
        public ICommand SwapScreenCommand { get; private set; }

        public string Name => ProcessWindow?.Name;

        private ProcessWindow _ProcessWindow;
        public ProcessWindow ProcessWindow
        {
            get => _ProcessWindow;
            set
            {
                _ProcessWindow = value;

                // refresh all properties
                OnPropertyChanged(string.Empty);
            }
        }

        public ProcessWindowViewModel(ProcessWindow processWindow)
        {
            ProcessWindow = processWindow;
            ProcessWindow.Refreshed += ProcessRefreshed;
            ProcessWindow.Disposed += ProcessDisposed;

            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;

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

                QuickApplicationsPageViewModel viewModel = OverlayQuickTools.GetCurrent().applicationsPage.DataContext as QuickApplicationsPageViewModel;
                WinAPI.MakeBorderless(ProcessWindow.Hwnd, viewModel.BorderlessEnabled && viewModel.BorderlessToggle);
                WinAPI.MoveWindow(ProcessWindow.Hwnd, screen, viewModel.windowPositions);
                WinAPI.SetForegroundWindow(ProcessWindow.Hwnd);

                // Todo: move me !
                Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true);
                profile = ManagerFactory.profileManager.GetProfileForSubProfile(profile);
                if (!profile.Default)
                {
                    profile.WindowsSettings[processWindow.Name] = new(screen.DeviceName, viewModel.BorderlessEnabled && viewModel.BorderlessToggle, viewModel.windowPositions);
                }
            });

            SwapScreenCommand = new DelegateCommand(async () =>
            {
                Screen screen = Screen.AllScreens.Where(screen => screen.DeviceName != CurrentScreen.DeviceName).FirstOrDefault();
                if (screen is null)
                    return;

                QuickApplicationsPageViewModel viewModel = OverlayQuickTools.GetCurrent().applicationsPage.DataContext as QuickApplicationsPageViewModel;
                WinAPI.MoveWindow(ProcessWindow.Hwnd, screen, WpfScreenHelper.Enum.WindowPositions.Maximize);
                WinAPI.SetForegroundWindow(ProcessWindow.Hwnd);

                // Todo: move me !
                Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
                if (!profile.Default)
                {
                    profile.WindowsSettings[processWindow.Name] = new(screen.DeviceName, viewModel.BorderlessEnabled && viewModel.BorderlessToggle, viewModel.windowPositions);
                }

                OnPropertyChanged(nameof(IsPrimaryScreen));
            });
        }

        private void ProcessDisposed(object? sender, EventArgs e)
        {
            Dispose();
        }

        private void MultimediaManager_DisplaySettingsChanged(Managers.Desktop.DesktopScreen screen, Managers.Desktop.ScreenResolution resolution)
        {
            if (ProcessWindow is null)
                return;

            // update current screen
            CurrentScreen = Screen.FromHandle(ProcessWindow.Hwnd);

            OnPropertyChanged(nameof(IsPrimaryScreen));
            OnPropertyChanged(nameof(HasTwoScreen));
        }

        private void ProcessRefreshed(object? sender, EventArgs e)
        {
            if (ProcessWindow is null)
                return;

            // update current screen
            CurrentScreen = Screen.FromHandle(ProcessWindow.Hwnd);

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPrimaryScreen));
        }

        public override void Dispose()
        {
            if (ProcessWindow is not null)
                ProcessWindow.Refreshed -= ProcessRefreshed;

            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;

            // dispose commands
            BringProcessCommand = null;
            SwapScreenCommand = null;

            ProcessWindow = null;

            base.Dispose();
        }
    }
}
