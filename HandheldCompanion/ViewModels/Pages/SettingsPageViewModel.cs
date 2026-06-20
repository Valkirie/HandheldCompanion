using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using static HandheldCompanion.Managers.UpdateManager;

namespace HandheldCompanion.ViewModels
{
    public class SettingsPageViewModel : BaseViewModel
    {
        #region Update status

        private string _updateStatusText = Properties.Resources.SettingsPage_UpToDate;
        public string UpdateStatusText
        {
            get => _updateStatusText;
            private set { if (_updateStatusText != value) { _updateStatusText = value; OnPropertyChanged(nameof(UpdateStatusText)); } }
        }

        private string _updateDateText = Properties.Resources.SettingsPage_LastChecked;
        public string UpdateDateText
        {
            get => _updateDateText;
            private set { if (_updateDateText != value) { _updateDateText = value; OnPropertyChanged(nameof(UpdateDateText)); } }
        }

        private Visibility _updateDateVisibility = Visibility.Visible;
        public Visibility UpdateDateVisibility
        {
            get => _updateDateVisibility;
            private set { if (_updateDateVisibility != value) { _updateDateVisibility = value; OnPropertyChanged(nameof(UpdateDateVisibility)); } }
        }

        private Visibility _updateSymbolVisibility = Visibility.Collapsed;
        public Visibility UpdateSymbolVisibility
        {
            get => _updateSymbolVisibility;
            private set { if (_updateSymbolVisibility != value) { _updateSymbolVisibility = value; OnPropertyChanged(nameof(UpdateSymbolVisibility)); } }
        }

        private Visibility _progressBarVisibility = Visibility.Collapsed;
        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            private set
            {
                if (_progressBarVisibility != value)
                {
                    _progressBarVisibility = value;
                    OnPropertyChanged(nameof(ProgressBarVisibility));
                    OnPropertyChanged(nameof(IsProgressBarActive));
                }
            }
        }

        public bool IsProgressBarActive => _progressBarVisibility == Visibility.Visible;

        private Visibility _changelogVisibility = Visibility.Collapsed;
        public Visibility ChangelogVisibility
        {
            get => _changelogVisibility;
            private set { if (_changelogVisibility != value) { _changelogVisibility = value; OnPropertyChanged(nameof(ChangelogVisibility)); } }
        }

        private string _changelogText = string.Empty;
        public string ChangelogText
        {
            get => _changelogText;
            private set { if (_changelogText != value) { _changelogText = value; OnPropertyChanged(nameof(ChangelogText)); } }
        }

        private bool _checkUpdateEnabled = true;
        public bool CheckUpdateEnabled
        {
            get => _checkUpdateEnabled;
            private set { if (_checkUpdateEnabled != value) { _checkUpdateEnabled = value; OnPropertyChanged(nameof(CheckUpdateEnabled)); } }
        }

        #endregion

        #region Update files

        public ObservableCollection<GithubUpdateViewModel> UpdateFiles { get; } = new();

        #endregion

        #region Commands

        public DelegateCommand CheckUpdateCommand { get; }

        #endregion

        #region DSU status

        private string _dsuStatusText = "Stopped";
        public string DSUStatusText
        {
            get => _dsuStatusText;
            private set { if (_dsuStatusText != value) { _dsuStatusText = value; OnPropertyChanged(nameof(DSUStatusText)); } }
        }

        private string _dsuClientCountText = string.Empty;
        public string DSUClientCountText
        {
            get => _dsuClientCountText;
            private set { if (_dsuClientCountText != value) { _dsuClientCountText = value; OnPropertyChanged(nameof(DSUClientCountText)); } }
        }

        private Visibility _dsuClientCountVisibility = Visibility.Collapsed;
        public Visibility DSUClientCountVisibility
        {
            get => _dsuClientCountVisibility;
            private set { if (_dsuClientCountVisibility != value) { _dsuClientCountVisibility = value; OnPropertyChanged(nameof(DSUClientCountVisibility)); } }
        }

        #endregion

        #region VIIPER status

        private string _viiperStatusText = "Stopped";
        public string VIIPERStatusText
        {
            get => _viiperStatusText;
            private set { if (_viiperStatusText != value) { _viiperStatusText = value; OnPropertyChanged(nameof(VIIPERStatusText)); } }
        }

        #endregion

        #region Constructor

        public SettingsPageViewModel()
        {
            BindingOperations.EnableCollectionSynchronization(UpdateFiles, _collectionLock);

            CheckUpdateCommand = new DelegateCommand(async () => await UpdateManager.StartProcess(false));

            UpdateManager.Updated += UpdateManager_Updated;

            // replay last known status
            UpdateDateText = Properties.Resources.SettingsPage_LastChecked + UpdateManager.GetTime();

            // DSU server events
            DSUServer.Started += DSUServer_Started;
            DSUServer.Stopped += DSUServer_Stopped;
            DSUServer.Failed += DSUServer_Failed;
            DSUServer.ClientsChanged += DSUServer_ClientsChanged;

            ViiperServerManager.Started += ViiperServerManager_Started;
            ViiperServerManager.Stopped += ViiperServerManager_Stopped;
            ViiperServerManager.Failed += ViiperServerManager_Failed;

            // replay current DSU state
            if (DSUServer.IsInitialized)
                DSUServer_Started();

            if (ViiperServerManager.IsRunning)
                ViiperServerManager_Started();
        }

        #endregion

        #region UpdateManager events

        private void UpdateManager_Updated(UpdateStatus status, UpdateFile? updateFile, object? value)
        {
            GithubUpdateViewModel? vm = updateFile is not null
                ? FindVm(updateFile)
                : null;

            switch (status)
            {
                case UpdateStatus.Initialized:
                case UpdateStatus.Updated:
                    UpdateStatusText = Properties.Resources.SettingsPage_UpToDate;
                    UpdateDateText = Properties.Resources.SettingsPage_LastChecked + UpdateManager.GetTime();
                    UpdateDateVisibility = Visibility.Visible;
                    UpdateSymbolVisibility = Visibility.Visible;
                    ProgressBarVisibility = Visibility.Collapsed;
                    CheckUpdateEnabled = true;
                    vm?.ResetToAvailable();
                    break;

                case UpdateStatus.Failed:
                    UpdateDateVisibility = Visibility.Visible;
                    UpdateSymbolVisibility = Visibility.Visible;
                    ProgressBarVisibility = Visibility.Collapsed;
                    CheckUpdateEnabled = true;
                    vm?.OnFailed();
                    break;

                case UpdateStatus.Checking:
                    lock (_collectionLock) { UpdateFiles.Clear(); }
                    if (value is bool background && background)
                        break;

                    UpdateStatusText = Properties.Resources.SettingsPage_UpdateCheck;
                    ChangelogVisibility = Visibility.Collapsed;
                    ChangelogText = string.Empty;
                    UpdateDateVisibility = Visibility.Collapsed;
                    UpdateSymbolVisibility = Visibility.Collapsed;
                    ProgressBarVisibility = Visibility.Visible;
                    CheckUpdateEnabled = false;
                    break;

                case UpdateStatus.Changelog:
                    ChangelogVisibility = Visibility.Visible;
                    UpdateDateVisibility = Visibility.Visible;
                    ChangelogText = value as string ?? string.Empty;
                    CheckUpdateEnabled = true;
                    break;

                case UpdateStatus.Ready:
                    ProgressBarVisibility = Visibility.Collapsed;
                    UpdateStatusText = Properties.Resources.SettingsPage_UpdateAvailable;
                    if (value is Dictionary<string, UpdateFile> files)
                    {
                        lock (_collectionLock)
                        {
                            UpdateFiles.Clear();
                            foreach (var file in files.Values)
                            {
                                var fileVm = new GithubUpdateViewModel(file);
                                fileVm.OnInstallFailed += OnInstallFailed;
                                UpdateFiles.Add(fileVm);
                            }
                        }
                    }
                    break;

                case UpdateStatus.ControllerDbReady:
                    if (updateFile is not null && FindVm(updateFile) is null)
                    {
                        UpdateSymbolVisibility = Visibility.Visible;
                        lock (_collectionLock)
                        {
                            var fileVm = new GithubUpdateViewModel(updateFile);
                            fileVm.OnInstallFailed += OnInstallFailed;
                            fileVm.OnInstalled += OnInstalled;
                            UpdateFiles.Add(fileVm);
                        }
                    }
                    break;

                case UpdateStatus.Download:
                    vm?.OnDownloadStarted();
                    break;

                case UpdateStatus.Downloading:
                    if (value is int percent)
                        vm?.OnProgressChanged(percent);
                    break;

                case UpdateStatus.Downloaded:
                    vm?.OnDownloadCompleted();
                    CheckUpdateEnabled = true;
                    break;
            }
        }

        private GithubUpdateViewModel? FindVm(UpdateFile updateFile)
        {
            lock (_collectionLock)
            {
                foreach (var vm in UpdateFiles)
                    if (vm.UpdateFile == updateFile)
                        return vm;
            }
            return null;
        }

        private async void OnInstallFailed(GithubUpdateViewModel vm)
        {
            await new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.SettingsPage_UpdateWarning,
                Content = Properties.Resources.SettingsPage_UpdateFailedInstall,
                PrimaryButtonText = Properties.Resources.ProfilesPage_OK
            }.ShowAsync();
        }

        private void OnInstalled(GithubUpdateViewModel vm)
        {
            lock (_collectionLock)
            {
                UpdateFiles.Remove(vm);
            }
        }

        #endregion

        #region DSUServer events

        private void DSUServer_Started()
        {
            DSUStatusText = $"Listening on port {DSUServer.serverPort}";
            DSUClientCountVisibility = Visibility.Visible;
            DSUServer_ClientsChanged(DSUServer.ConnectedClientCount);
        }

        private void DSUServer_Stopped()
        {
            DSUStatusText = "Stopped";
            DSUClientCountVisibility = Visibility.Collapsed;
            DSUClientCountText = string.Empty;
        }

        private void DSUServer_Failed(int port)
        {
            DSUStatusText = $"Failed to bind port {port}";
            DSUClientCountVisibility = Visibility.Collapsed;
            DSUClientCountText = string.Empty;
        }

        private void DSUServer_ClientsChanged(int count)
        {
            DSUClientCountText = count == 1 ? "1 connected client" : $"{count} connected clients";
        }

        #endregion

        #region ViiperServerManager events

        private void ViiperServerManager_Started()
        {
            VIIPERStatusText = $"Listening on {ViiperServerManager.Host}:{ViiperServerManager.Port}";
        }

        private void ViiperServerManager_Stopped()
        {
            VIIPERStatusText = "Stopped";
        }

        private void ViiperServerManager_Failed(string reason)
        {
            VIIPERStatusText = reason;
        }

        #endregion

        #region Dispose

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UpdateManager.Updated -= UpdateManager_Updated;
                DSUServer.Started -= DSUServer_Started;
                DSUServer.Stopped -= DSUServer_Stopped;
                DSUServer.Failed -= DSUServer_Failed;
                DSUServer.ClientsChanged -= DSUServer_ClientsChanged;
                ViiperServerManager.Started -= ViiperServerManager_Started;
                ViiperServerManager.Stopped -= ViiperServerManager_Stopped;
                ViiperServerManager.Failed -= ViiperServerManager_Failed;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
