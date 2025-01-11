using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels.Commands;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.ViewModels
{
    public class QuickApplicationsPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProcessExViewModel> Processes { get; set; } = [];
        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = [];

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
                return windowPositions == WindowPositions.Maximize;
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

        public QuickApplicationsPageViewModel()
        {
            RadioButtonCheckedCommand = new RelayCommand(OnRadioButtonChecked);

            // manage events
            ProcessManager.ProcessStarted += ProcessStarted;
            ProcessManager.ProcessStopped += ProcessStopped;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Processes, new object());

            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Profiles, new object());
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            // ignore me
            if (profile.Default)
                return;

            ProfileViewModel? foundProfile = Profiles.ToList().FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
            if (foundProfile is not null)
            {
                Profiles.SafeRemove(foundProfile);
                foundProfile.Dispose();
            }
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // ignore me
            if (profile.Default)
                return;

            ProfileViewModel? foundProfile = Profiles.ToList().FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
            if (foundProfile is null)
            {
                if (profile.IsPinned)
                    Profiles.SafeAdd(new ProfileViewModel(profile, this));
            }
            else
            {
                if (profile.IsPinned)
                    foundProfile.Profile = profile;
                else
                    ProfileManager_Deleted(profile);
            }
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

            ProcessExViewModel? foundProcess = Processes.ToList().FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
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

            ProcessExViewModel? foundProcess = Processes.ToList().FirstOrDefault(p => p.Process == processEx || p.Process.ProcessId == processEx.ProcessId);
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
            ProcessManager.ProcessStarted -= ProcessStarted;
            ProcessManager.ProcessStopped -= ProcessStopped;
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;

            base.Dispose();
        }
    }
}
