using HandheldCompanion.Extensions;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace HandheldCompanion.ViewModels
{
    public class ProcessExViewModel : BaseViewModel
    {
        public QuickApplicationsPageViewModel PageViewModel;

        private readonly object _processWindowsSyncLock = new object();
        public ObservableCollection<ProcessWindowViewModel> ProcessWindows { get; set; } = [];

        private ProcessEx _process;
        public ProcessEx Process
        {
            get => _process;
            set
            {
                // todo: we need to check if _hotkey != value but this will return false because this is a pointer
                // I've implemented all required Clone() functions but not sure where to call them

                UpdateProcess(_process, value);

                // refresh all properties
                OnPropertyChanged(string.Empty);
            }
        }

        public ImageSource Icon => Process.ProcessIcon;

        public bool IsSuspended
        {
            get => Process.IsSuspended;
            set
            {
                if (value != IsSuspended)
                {
                    Process.IsSuspended = value;
                    OnPropertyChanged(nameof(IsSuspended));
                }
            }
        }

        public string Executable => Process.Executable;
        public string Path => Process.Path;

        public string ProductName
        {
            get
            {
                string FileDescription = Process.AppProperties.TryGetValue("FileDescription", out var property) ? property : string.Empty;
                if (!string.IsNullOrEmpty(FileDescription))
                    return FileDescription;

                string pName = Process.Process.MainModule?.FileVersionInfo?.ProductName ?? string.Empty;
                if (!string.IsNullOrEmpty(pName))
                    return pName;

                return Executable;
            }
        }

        public string CompanyName
        {
            get
            {
                string Copyright = Process.AppProperties.TryGetValue("Copyright", out var property) ? property : string.Empty;
                if (!string.IsNullOrEmpty(Copyright))
                    return Copyright;

                string cName = Process.Process.MainModule?.FileVersionInfo?.CompanyName ?? string.Empty;
                if (!string.IsNullOrEmpty(cName))
                    return cName;

                return Path;
            }
        }

        public bool FullScreenOptimization => !Process.FullScreenOptimization;
        public bool HighDPIAware => !Process.HighDPIAware;

        public ICommand KillProcessCommand { get; private set; }

        public ProcessExViewModel(ProcessEx process, QuickApplicationsPageViewModel pageViewModel)
        {
            Process = process;
            Process.WindowAttached += Process_WindowAttached;
            Process.WindowDetached += Process_WindowDetached;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ProcessWindows, _processWindowsSyncLock);

            foreach (ProcessWindow processWindow in Process.ProcessWindows.Values)
            {
                if (string.IsNullOrEmpty(processWindow.Name))
                    continue;

                Process_WindowAttached(processWindow);
            }

            PageViewModel = pageViewModel;

            KillProcessCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(OverlayQuickTools.GetCurrent())
                {
                    Title = "Terminate application",
                    Content = string.Format("Do you want to end the application '{0}'?", Executable),
                    DefaultButton = ContentDialogButton.Close,
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes,
                };

                Task<ContentDialogResult> dialogTask = dialog.ShowAsync();
                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        Process.Kill();
                        break;
                    default:
                        dialog.Hide();
                        break;
                }
            });
        }

        private void Process_WindowAttached(ProcessWindow processWindow)
        {
            ProcessWindowViewModel? foundWindow = ProcessWindows.FirstOrDefault(win => win.ProcessWindow.Hwnd == processWindow.Hwnd);
            if (foundWindow is null)
            {
                ProcessWindows.SafeAdd(new ProcessWindowViewModel(processWindow));
            }
            else
            {
                foundWindow.ProcessWindow = processWindow;
            }
        }

        private void Process_WindowDetached(ProcessWindow processWindow)
        {
            ProcessWindowViewModel? foundWindow = ProcessWindows.FirstOrDefault(win => win.ProcessWindow.Hwnd == processWindow.Hwnd);
            if (foundWindow is not null)
            {
                ProcessWindows.SafeRemove(foundWindow);
                foundWindow.Dispose();
            }
        }

        private void UpdateProcess(ProcessEx oldProcess, ProcessEx newProcess)
        {
            if (oldProcess is not null)
                oldProcess.Refreshed -= ProcessRefreshed;

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
            {
                _process.Refreshed -= ProcessRefreshed;
                _process.WindowAttached -= Process_WindowAttached;
                _process.WindowDetached -= Process_WindowDetached;
            }

            // Take a snapshot of the children and clear the live collection
            ProcessWindowViewModel[] windowsSnapshot;
            lock (_processWindowsSyncLock)
            {
                windowsSnapshot = ProcessWindows.ToArray();
                ProcessWindows.SafeClear();    // direct Clear, not SafeClear
            }

            // Dispose each window from the snapshot (outside the lock)
            foreach (ProcessWindowViewModel processWindow in windowsSnapshot)
                processWindow.Dispose();

            // dispose commands
            KillProcessCommand = null;
            PageViewModel = null;
            _process = null;

            base.Dispose();
        }
    }
}
