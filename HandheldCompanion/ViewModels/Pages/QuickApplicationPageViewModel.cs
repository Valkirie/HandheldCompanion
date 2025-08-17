using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.ViewModels
{
    public class QuickApplicationsPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProcessExViewModel> Processes { get; set; } = new();
        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = new();
        public ObservableCollection<ProfileViewModel> PagedProfiles { get; } = new();

        private const int PageSize = 8;
        public int TotalPages => (int)Math.Ceiling((double)Profiles.Count / PageSize);

        public ICommand RadioButtonCheckedCommand { get; }

        private WindowPositions _windowPositions = WindowPositions.Maximize;
        public WindowPositions windowPositions
        {
            get
            {
                return _windowPositions;
            }
            set
            {
                if (value != _windowPositions)
                {
                    _windowPositions = value;
                    OnPropertyChanged(nameof(windowPositions));
                    OnPropertyChanged(nameof(BorderlessEnabled));
                }
            }
        }

        private bool _BorderlessEnabled = false;
        public bool BorderlessEnabled
        {
            get
            {
                return true; // return windowPositions == WindowPositions.Maximize;
            }
        }

        private bool _BorderlessToggle = false;
        public bool BorderlessToggle
        {
            get => _BorderlessToggle;
            set
            {
                if (value != _BorderlessToggle)
                {
                    _BorderlessToggle = value;
                    OnPropertyChanged(nameof(BorderlessToggle));
                }
            }
        }

        private int _selectedPageIndex;
        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set
            {
                if (_selectedPageIndex != value && value >= 0 && value < TotalPages)
                {
                    _selectedPageIndex = value;
                    OnPropertyChanged(nameof(SelectedPageIndex));
                    RefreshPage();
                }
            }
        }

        private void RefreshPage()
        {
            PagedProfiles.Clear();

            // order all profiles by LastUsed (newest first)
            var items = Profiles
                .OrderByDescending(p => p.LastUsed)
                .Skip(SelectedPageIndex * PageSize)
                .Take(PageSize);

            foreach (var vm in items)
                PagedProfiles.Add(vm);
        }

        public bool IsReady => ManagerFactory.processManager.IsReady;

        public QuickApplicationsPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Processes, new object());
            BindingOperations.EnableCollectionSynchronization(Profiles, new object());
            Profiles.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(TotalPages));

                // clamp page index if we deleted the last page
                if (SelectedPageIndex >= TotalPages)
                    SelectedPageIndex = TotalPages - 1;

                RefreshPage();
            };

            RadioButtonCheckedCommand = new RelayCommand(OnRadioButtonChecked);

            // raise events
            switch (ManagerFactory.processManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryForeground();
                    break;
            }

            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;

            // first‐time fill
            RefreshPage();
        }

        private void QueryForeground()
        {
            // manage events
            ManagerFactory.processManager.ProcessStarted += ProcessStarted;
            ManagerFactory.processManager.ProcessStopped += ProcessStopped;

            foreach (ProcessEx processEx in ProcessManager.GetProcesses().Where(p => p.Filter != ProcessEx.ProcessFilter.HandheldCompanion))
                ProcessStarted(processEx, true);

            OnPropertyChanged(nameof(IsReady));
        }

        private void ProcessManager_Initialized()
        {
            QueryForeground();
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            // ignore me
            if (profile.Default)
                return;

            ProfileViewModel? foundProfile = Profiles.FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
            if (foundProfile is not null)
            {
                Profiles.SafeRemove(foundProfile);
                foundProfile.Dispose();

                // re-compute pages
                OnPropertyChanged(nameof(TotalPages));
                UIHelper.TryInvoke(RefreshPage);
            }
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // ignore me
            if (profile.Default)
                return;

            ProfileViewModel? foundProfile = Profiles.FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
            if (foundProfile is null)
            {
                if (profile.IsPinned)
                    Profiles.SafeAdd(new ProfileViewModel(profile, true));
            }
            else
            {
                if (profile.IsPinned)
                    foundProfile.Profile = profile;
                else
                    ProfileManager_Deleted(profile);
            }

            // re-compute pages
            OnPropertyChanged(nameof(TotalPages));
            UIHelper.TryInvoke(RefreshPage);
        }

        private void OnRadioButtonChecked(object parameter)
        {
            if (parameter is string paramString && int.TryParse(paramString, out int value))
                windowPositions = (WindowPositions)value;
        }

        private void ProcessStopped(ProcessEx processEx)
        {
            if (processEx is null)
                return;

            ProcessExViewModel? foundProcess = Processes.FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
            if (foundProcess is not null)
            {
                Processes.SafeRemove(foundProcess);
                foundProcess.Dispose();
            }
        }

        private void ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            if (processEx is null)
                return;

            switch (processEx.Filter)
            {
                // prevent critical processes from being listed
                case ProcessEx.ProcessFilter.Restricted:
                    return;
            }

            ProcessExViewModel? foundProcess = Processes.FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
            if (foundProcess is null)
            {
                Processes.SafeAdd(new ProcessExViewModel(processEx, this));
            }
            else
            {
                // Some apps might have the process come in twice, update the process on the viewmodel
                foundProcess.Process = processEx;
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.processManager.ProcessStarted -= ProcessStarted;
            ManagerFactory.processManager.ProcessStopped -= ProcessStopped;
            ManagerFactory.processManager.Initialized -= ProcessManager_Initialized;
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;

            base.Dispose();
        }
    }
}
