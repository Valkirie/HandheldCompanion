using HandheldCompanion.Controls;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using System;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class ProcessExViewModel : BaseViewModel
    {
        private ProcessEx _process;
        public ProcessEx Process
        {
            get => _process;
            set
            {
                if (value != _process)
                {
                    UpdateProcess(_process, value);
                    OnPropertyChanged(nameof(Process));
                }
            }
        }

        public string Title => Process.MainWindowTitle;

        public ImageSource ProcessIcon => Process.ProcessIcon;

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

        public ProcessExViewModel(ProcessEx process)
        {
            Process = process;

            KillProcessCommand = new DelegateCommand(() =>
            {
                Process.Process?.Kill();
            });

            BringProcessCommand = new DelegateCommand(() =>
            {
                // Get the screen where the reference window is located
                var screen = System.Windows.Forms.Screen.FromHandle(OverlayQuickTools.GetCurrent().hwndSource.Handle);

                // Get the working area of the screen
                var workingArea = screen.WorkingArea;

                // Move the window to the new screen and maximize it
                ProcessUtils.ShowWindow(Process.MainWindowHandle, 9);
                WinAPI.SetWindowPos(Process.MainWindowHandle, IntPtr.Zero, workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height, WinAPI.SWP_SHOWWINDOW | WinAPI.SWP_FRAMECHANGED);
                ProcessUtils.ShowWindow(Process.MainWindowHandle, 3);
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
