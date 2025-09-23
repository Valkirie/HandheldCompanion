using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.ViewModels.Misc
{
    public class WindowListItemViewModel : BaseViewModel
    {
        private ProcessWindow _processWindow;       // when live window
        private ProcessWindowSettings _settings;    // when not live
        private Screen _currentScreen = Screen.PrimaryScreen;

        public ProcessWindow ProcessWindow
        {
            get => _processWindow;
            set
            {
                if (_processWindow == value) return;

                UnhookLiveEvents(_processWindow);

                _processWindow = value;

                if (_processWindow != null)
                {
                    // mirror live settings
                    _settings = _processWindow.windowSettings ?? _settings ?? new ProcessWindowSettings();
                    _currentScreen = Screen.FromHandle(_processWindow.Hwnd) ?? Screen.PrimaryScreen;

                    HookLiveEvents(_processWindow);
                }

                RaiseAll();
            }
        }

        public ProcessWindowSettings Settings
        {
            get => _processWindow?.windowSettings ?? (_settings ??= new ProcessWindowSettings());
            private set
            {
                _settings = value ?? new ProcessWindowSettings();
                RaiseAll();
            }
        }

        private readonly string _savedName;
        public string Name => _savedName ?? _processWindow?.Name ?? "Unknown window";

        public bool IsPresent => _processWindow != null;
        public int Hwnd => _processWindow?.Hwnd ?? (Settings?.Hwnd ?? 0);

        public bool HasTwoScreen => Screen.AllScreens.Length > 1;
        public bool IsPrimaryScreen => _currentScreen?.Primary ?? true;

        public bool HasDisplay1 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY1", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay2 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY2", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay3 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY3", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay4 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY4", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay5 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY5", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay6 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY6", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay7 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY7", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay8 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY8", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay9 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY9", StringComparison.OrdinalIgnoreCase));
        public bool HasDisplay10 => Screen.AllScreens.Any(s => s.DeviceName.Equals(@"\\.\DISPLAY10", StringComparison.OrdinalIgnoreCase));

        public int TargetDisplay
        {
            get
            {
                string deviceName = Settings?.DeviceName ?? @"\\.\DISPLAY0";
                char last = deviceName.Length > 0 ? deviceName[^1] : '0';
                return int.TryParse(last.ToString(), out int idx) ? idx : 0;
            }
            set
            {
                if (value == TargetDisplay) return;

                // Build the device name from index
                string device = $@"\\.\DISPLAY{value}";

                if (IsPresent)
                {
                    // Live: move via WindowManager (same as old VM)
                    Screen screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName.Equals(device, StringComparison.OrdinalIgnoreCase));
                    if (screen != null)
                    {
                        WindowManager.SetTargetDisplay(_processWindow, screen);
                        OnPropertyChanged(nameof(TargetDisplay));
                        OnPropertyChanged(nameof(DeviceName));
                    }
                }
                else
                {
                    // Not live: just store for later
                    if (!string.Equals(Settings.DeviceName, device, StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.DeviceName = device;
                        OnPropertyChanged(nameof(TargetDisplay));
                        OnPropertyChanged(nameof(DeviceName));
                    }
                }
            }
        }

        public string DeviceName => Settings?.DeviceName ?? @"\\.\DISPLAY0";

        public int TargetWindowPosition
        {
            get
            {
                return (int)(Settings?.WindowPositions ?? WindowPositions.Center);
            }
            set
            {
                if (value == TargetWindowPosition) return;

                var newPos = (WindowPositions)value;

                if (IsPresent)
                {
                    WindowManager.SetTargetWindowPosition(_processWindow, newPos);
                    OnPropertyChanged(nameof(TargetWindowPosition));
                }
                else
                {
                    Settings.WindowPositions = newPos;
                    OnPropertyChanged(nameof(TargetWindowPosition));
                }
            }
        }

        public bool Borderless
        {
            get => Settings?.Borderless ?? false;
            set
            {
                if (value == Borderless) return;

                if (IsPresent)
                {
                    WindowManager.SetBorderless(_processWindow, value);
                    OnPropertyChanged(nameof(Borderless));
                }
                else
                {
                    Settings.Borderless = value;
                    OnPropertyChanged(nameof(Borderless));
                }
            }
        }

        public ICommand BringProcessCommand { get; private set; }
        public ICommand SwapScreenCommand { get; private set; }

        public WindowListItemViewModel(ProcessWindow processWindow)
        {
            _savedName = null;
            Settings = processWindow?.windowSettings ?? new ProcessWindowSettings();
            ProcessWindow = processWindow;

            InitDisplayEventHooks();
            InitCommands();
        }

        public WindowListItemViewModel(string name, ProcessWindowSettings saved)
        {
            _savedName = name;
            Settings = saved ?? new ProcessWindowSettings();
            ProcessWindow = null;

            InitDisplayEventHooks();
            InitCommands();
        }

        public void UpdateFrom(ProcessWindow processWindow)
        {
            ProcessWindow = processWindow;
        }

        private void InitCommands()
        {
            BringProcessCommand = new DelegateCommand(async () =>
            {
                if (!IsPresent) return;

                OverlayQuickTools qtWindow = OverlayQuickTools.GetCurrent();
                ContentDialogResult result = await qtWindow.applicationsPage.SnapDialog.ShowAsync(qtWindow);
                if (result == ContentDialogResult.None)
                {
                    qtWindow.applicationsPage.SnapDialog.Hide();
                    return;
                }

                Screen screen = Screen.FromHandle(_processWindow.Hwnd);
                if (screen is null) return;

                var viewModel = (QuickApplicationsPageViewModel)OverlayQuickTools.GetCurrent().applicationsPage.DataContext;
                WindowManager.SetWindowSettings(_processWindow, screen, viewModel.BorderlessEnabled && viewModel.BorderlessToggle, viewModel.windowPositions);
            });

            SwapScreenCommand = new DelegateCommand(() =>
            {
                if (!IsPresent) return;

                Screen next = Screen.AllScreens.FirstOrDefault(s => s.DeviceName != _currentScreen.DeviceName);
                if (next is null) return;

                WindowManager.SetWindowSettings(_processWindow, next, false, WindowPositions.Maximize);
                OnPropertyChanged(nameof(IsPrimaryScreen));
            });
        }

        private void InitDisplayEventHooks()
        {
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
            ManagerFactory.multimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        }

        private void HookLiveEvents(ProcessWindow pw)
        {
            if (pw == null) return;
            pw.Refreshed += Process_Refreshed;
            pw.Disposed += Process_Disposed;
        }

        private void UnhookLiveEvents(ProcessWindow pw)
        {
            if (pw == null) return;
            pw.Refreshed -= Process_Refreshed;
            pw.Disposed -= Process_Disposed;
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(HasDisplay1));
            OnPropertyChanged(nameof(HasDisplay2));
            OnPropertyChanged(nameof(HasDisplay3));
            OnPropertyChanged(nameof(HasDisplay4));
            OnPropertyChanged(nameof(HasTwoScreen));
        }

        private void MultimediaManager_DisplaySettingsChanged(Managers.Desktop.DesktopScreen screen, Managers.Desktop.ScreenResolution resolution)
        {
            if (_processWindow != null)
            {
                _currentScreen = Screen.FromHandle(_processWindow.Hwnd) ?? _currentScreen;
                OnPropertyChanged(nameof(IsPrimaryScreen));
            }

            OnPropertyChanged(nameof(HasTwoScreen));
        }

        private void Process_Refreshed(object? sender, EventArgs e)
        {
            if (_processWindow is null) return;

            _currentScreen = Screen.FromHandle(_processWindow.Hwnd) ?? _currentScreen;

            // mirror live settings to keep Settings in sync
            _settings = _processWindow.windowSettings ?? _settings;

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPrimaryScreen));
            RaiseAll();
        }

        private void Process_Disposed(object? sender, EventArgs e)
        {
            // window went away -> keep saved settings and mark as not present
            ProcessWindow = null;
        }

        private void RaiseAll()
        {
            OnPropertyChanged(string.Empty); // full refresh for simplicity
        }

        public override void Dispose()
        {
            UnhookLiveEvents(_processWindow);

            // global hooks
            ManagerFactory.multimediaManager.DisplaySettingsChanged -= MultimediaManager_DisplaySettingsChanged;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

            BringProcessCommand = null;
            SwapScreenCommand = null;

            _processWindow = null;

            base.Dispose();
        }

        // Optional convenience to raise single props
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => base.OnPropertyChanged(name);
    }
}