using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;
using WpfScreenHelper.Enum;

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
        public int TargetDisplay
        {
            get
            {
                string deviceName = ProcessWindow?.windowSettings?.DeviceName ?? "\\\\.\\DISPLAY0";
                string deviceIndex = deviceName.Last().ToString();

                if (int.TryParse(deviceIndex, out int index))
                    return index;

                return 0;
            }
            set
            {
                if (value != TargetDisplay)
                {
                    Screen screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals($"\\\\.\\DISPLAY{value}"));
                    WindowManager.SetTargetDisplay(ProcessWindow, screen);
                }
            }
        }

        public int TargetWindowPosition
        {
            get
            {
                return (int)(ProcessWindow?.windowSettings?.WindowPositions ?? WindowPositions.Center);
            }
            set
            {
                if (value != TargetWindowPosition)
                {
                    WindowManager.SetTargetWindowPosition(ProcessWindow, (WindowPositions)value);
                }
            }
        }

        public bool Borderless
        {
            get
            {
                return ProcessWindow?.windowSettings?.Borderless ?? false;
            }
            set
            {
                if (value != Borderless)
                {
                    WindowManager.SetBorderless(ProcessWindow, value);
                }
            }
        }

        public bool HasDisplay1 => Screen.AllScreens.Any(screen => screen.DeviceName == "\\\\.\\DISPLAY1");
        public bool HasDisplay2 => Screen.AllScreens.Any(screen => screen.DeviceName == "\\\\.\\DISPLAY2");
        public bool HasDisplay3 => Screen.AllScreens.Any(screen => screen.DeviceName == "\\\\.\\DISPLAY3");
        public bool HasDisplay4 => Screen.AllScreens.Any(screen => screen.DeviceName == "\\\\.\\DISPLAY4");

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

            // manage events
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
            SystemEvents_DisplaySettingsChanged(null, null);

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

                // get page viewmodel
                QuickApplicationsPageViewModel viewModel = OverlayQuickTools.GetCurrent().applicationsPage.DataContext as QuickApplicationsPageViewModel;
                WindowManager.SetWindowSettings(processWindow, screen, viewModel.BorderlessEnabled && viewModel.BorderlessToggle, viewModel.windowPositions);
            });

            SwapScreenCommand = new DelegateCommand(async () =>
            {
                Screen screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != CurrentScreen.DeviceName);
                if (screen is null)
                    return;

                WindowManager.SetWindowSettings(processWindow, screen, false, WpfScreenHelper.Enum.WindowPositions.Maximize);

                OnPropertyChanged(nameof(IsPrimaryScreen));
            });
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(HasDisplay1));
            OnPropertyChanged(nameof(HasDisplay2));
            OnPropertyChanged(nameof(HasDisplay3));
            OnPropertyChanged(nameof(HasDisplay4));
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
            {
                ProcessWindow.Refreshed -= ProcessRefreshed;
                ProcessWindow.Disposed -= ProcessDisposed;
            }

            // manage events
            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            // dispose commands
            BringProcessCommand = null;
            SwapScreenCommand = null;

            ProcessWindow = null;

            base.Dispose();
        }
    }
}
