using HandheldCompanion.Controls;
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

        public ProcessExViewModel(ProcessEx process)
        {
            Process = process;

            KillProcessCommand = new DelegateCommand(() =>
            {
                Process.Process?.Kill();
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
        }

        public override void Dispose()
        {
            if (_process is not null)
                _process.Refreshed -= ProcessRefreshed;

            base.Dispose();
        }
    }
}
