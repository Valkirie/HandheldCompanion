using HandheldCompanion.Controls;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class ProcessExViewModel : BaseViewModel
    {
        QuickApplicationsPageViewModel PageViewModel;

        private ProcessEx _process;
        public ProcessEx Process
        {
            get => _process;
            set
            {
                // todo: we need to check if _hotkey != value but this will return false because this is a pointer
                // I've implemented all required Clone() functions but not sure where to call them

                UpdateProcess(_process, value);
                OnPropertyChanged(nameof(Process));
            }
        }

        public string Title => Process.MainWindowTitle;

        public ImageSource Icon => Process.ProcessIcon;

        public bool IsSuspended
        {
            get => Process.IsSuspended;
            set
            {
                if (value != IsSuspended)
                {
                    Process.IsSuspended = value;
                }
            }
        }

        public string Executable => Process.Executable;

        public bool FullScreenOptimization
        {
            get => !Process.FullScreenOptimization;
            set { } // empty set to allow binding to ToggleSwitch.IsOn
        }
        public bool HighDPIAware
        {
            get => !Process.HighDPIAware;
            set { } // empty set to allow binding to ToggleSwitch.IsOn
        }

        public ICommand KillProcessCommand { get; private set; }
        public ICommand BringProcessCommand { get; private set; }

        public ProcessExViewModel(ProcessEx process, QuickApplicationsPageViewModel pageViewModel)
        {
            Process = process;
            PageViewModel = pageViewModel;

            KillProcessCommand = new DelegateCommand(() =>
            {
                Process.Process?.Kill();
            });

            BringProcessCommand = new DelegateCommand(async () =>
            {
                OverlayQuickTools qtWindow = OverlayQuickTools.GetCurrent();

                ContentDialogResult result = await qtWindow.applicationsPage.ProfileRenameDialog.ShowAsync();
                switch(result)
                {
                    case ContentDialogResult.None:
                        qtWindow.applicationsPage.ProfileRenameDialog.Hide();
                        return;
                }

                // Get the screen where the reference window is located
                Screen screen = Screen.FromHandle(qtWindow.hwndSource.Handle);
                if (screen is null)
                    return;

                int style = WinAPI.GetWindowLong(Process.MainWindowHandle, WinAPI.GWL_STYLE);
                if (PageViewModel.BorderlessEnabled && PageViewModel.BorderlessToggle)
                {
                    WinAPI.SetWindowLong(Process.MainWindowHandle, WinAPI.GWL_STYLE, (style & ~WinAPI.WS_BORDER & ~WinAPI.WS_CAPTION & ~WinAPI.WS_SYSMENU));
                }
                else if ((style & WinAPI.WS_BORDER) == 0 && (style & WinAPI.WS_CAPTION) == 0)
                {
                    WinAPI.SetWindowLong(Process.MainWindowHandle, WinAPI.GWL_STYLE, (style | WinAPI.WS_BORDER | WinAPI.WS_CAPTION | WinAPI.WS_SYSMENU));
                }

                WinAPI.MoveWindow(Process.MainWindowHandle, screen, PageViewModel.windowPositions);
            });
        }

        private void UpdateProcess(ProcessEx oldProcess, ProcessEx newProcess)
        {
            if (oldProcess is not null)
            {
                oldProcess.Refreshed -= ProcessRefreshed;
            }

            newProcess.Refreshed += ProcessRefreshed;

            _process = newProcess;
        }

        private void ProcessRefreshed(object? sender, EventArgs e)
        {
            // Property Changed is called here as processes are refreshed at an interval
            OnPropertyChanged(nameof(IsSuspended));
            OnPropertyChanged(nameof(FullScreenOptimization));
            OnPropertyChanged(nameof(HighDPIAware));
            OnPropertyChanged(nameof(Title));
        }

        public override void Dispose()
        {
            if (_process is not null)
                _process.Refreshed -= ProcessRefreshed;

            base.Dispose();
        }
    }
}
