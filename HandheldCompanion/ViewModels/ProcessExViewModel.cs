using HandheldCompanion.Extensions;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views;
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
        public ObservableCollection<WindowListItemViewModel> ProcessWindows { get; set; } = [];

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

        public bool IsRunning => Process?.Process != null && !Process.Process.HasExited;
        public bool CanSuspend => IsRunning && !IsSuspended;
        public bool CanResume => IsRunning && IsSuspended;

        public ICommand SuspendProcessCommand { get; private set; }
        public ICommand ResumeProcessCommand { get; private set; }
        public ICommand KillProcessCommand { get; private set; }

        public readonly bool IsQuickTools;
        public bool IsMainPage => !IsQuickTools;

        public ProcessExViewModel(ProcessEx process, bool isQuickTools)
        {
            Process = process;
            Process.WindowAttached += Process_WindowAttached;
            Process.WindowDetached += Process_WindowDetached;

            IsQuickTools = isQuickTools;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ProcessWindows, _collectionLock);

            foreach (ProcessWindow processWindow in Process.ProcessWindows.Values)
            {
                if (string.IsNullOrEmpty(processWindow.Name))
                    continue;

                Process_WindowAttached(processWindow);
            }

            SuspendProcessCommand = new DelegateCommand(async () =>
            {
                try
                {
                    bool success = await Managers.ProcessManager.SuspendProcess(Process);
                    if (success)
                    {
                        OnPropertyChanged(nameof(IsSuspended));
                        OnPropertyChanged(nameof(CanSuspend));
                        OnPropertyChanged(nameof(CanResume));
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to suspend process: {0}", ex.Message);
                }
            });

            ResumeProcessCommand = new DelegateCommand(async () =>
            {
                try
                {
                    bool success = await Managers.ProcessManager.ResumeProcess(Process);
                    if (success)
                    {
                        OnPropertyChanged(nameof(IsSuspended));
                        OnPropertyChanged(nameof(CanSuspend));
                        OnPropertyChanged(nameof(CanResume));
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to resume process: {0}", ex.Message);
                }
            });

            KillProcessCommand = new DelegateCommand(async () =>
            {
                Dialog dialog = new Dialog(isQuickTools ? OverlayQuickTools.GetCurrent() : MainWindow.GetCurrent());
                if (dialog == null)
                {
                    LogManager.LogWarning("Cannot show kill process dialog: no loaded window available");
                    return;
                }

                dialog.Title = "Terminate application";
                dialog.Content = string.Format("Do you want to end the application '{0}'?", Executable);
                dialog.DefaultButton = ContentDialogButton.Close;
                dialog.CloseButtonText = Properties.Resources.ProfilesPage_Cancel;
                dialog.PrimaryButtonText = Properties.Resources.ProfilesPage_Yes;

                Task<ContentDialogResult> dialogTask = dialog.ShowAsync();
                await dialogTask;

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
            lock (_collectionLock)
            {
                WindowListItemViewModel? foundWindow = ProcessWindows.FirstOrDefault(win => win.ProcessWindow?.Hwnd == processWindow.Hwnd);
                if (foundWindow is null)
                {
                    ProcessWindows.SafeAdd(new WindowListItemViewModel(processWindow));
                }
                else
                {
                    foundWindow.ProcessWindow = processWindow;
                }
            }
        }

        private void Process_WindowDetached(ProcessWindow processWindow)
        {
            lock (_collectionLock)
            {
                WindowListItemViewModel? foundWindow = ProcessWindows.FirstOrDefault(win => win.ProcessWindow?.Hwnd == processWindow.Hwnd);
                if (foundWindow is not null)
                {
                    ProcessWindows.SafeRemove(foundWindow);
                    foundWindow.Dispose();
                }
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
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanSuspend));
            OnPropertyChanged(nameof(CanResume));
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

            // Dispose each window from a snapshot
            foreach (WindowListItemViewModel processWindow in ProcessWindows.ToArray())
                processWindow.Dispose();

            // clear windows
            ProcessWindows.SafeClear();

            // dispose commands
            KillProcessCommand = null;
            _process = null;

            base.Dispose();
        }
    }
}
