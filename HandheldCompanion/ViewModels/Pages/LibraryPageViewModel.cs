using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class LibraryPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = [];
        public ICollectionView ProfilesView { get; }

        private bool _sortAscending = true;
        public bool SortAscending
        {
            get => _sortAscending;
            set
            {
                if (value != SortAscending)
                {
                    _sortAscending = value;
                    OnPropertyChanged(nameof(SortAscending));

                    UpdateSorting();
                }
            }
        }

        public ICommand ToggleSortCommand { get; }

        private Color _highlightColor = Colors.Red;
        public Color HighlightColor
        {
            get => _highlightColor;
            set
            {
                if (_highlightColor != value)
                {
                    _highlightColor = value;
                    OnPropertyChanged(nameof(HighlightColor));
                }
            }
        }

        private BitmapImage _Artwork;
        public BitmapImage Artwork
        {
            get => _Artwork;
            set
            {
                if (_Artwork != value)
                {
                    _Artwork = value;
                    OnPropertyChanged(nameof(Artwork));
                }
            }
        }

        public LibraryPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Profiles, new object());
            ProfilesView = CollectionViewSource.GetDefaultView(Profiles);
            ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.Name), ListSortDirection.Ascending));

            ToggleSortCommand = new DelegateCommand(() =>
            {
                SortAscending = !SortAscending;
            });

            // raise events
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }
        }

        private void QueryProfile()
        {
            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;

            // Load only the ones that should be shown
            foreach (Profile profile in ManagerFactory.profileManager.GetProfiles().Where(p => !p.Default))
            {
                ProfileManager_Updated(profile, UpdateSource.Background, false);

                foreach (Profile subProfile in ManagerFactory.profileManager.GetSubProfilesFromProfile(profile))
                    ProfileManager_Updated(subProfile, UpdateSource.Background, false);
            }
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void UpdateSorting()
        {
            ListSortDirection direction = SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            ProfilesView.SortDescriptions.Clear();
            ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.Name), direction));
            ProfilesView.Refresh();
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

            bool shouldShow = profile.ShowInLibrary;

            // find based on guid
            ProfileViewModel? existingVm = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

            if (shouldShow)
            {
                if (existingVm is null)
                {
                    // Not yet in list, add
                    Profiles.SafeAdd(new ProfileViewModel(profile, false));
                }
                else
                {
                    // Already in list, only update
                    existingVm.Profile = profile;
                }
            }
            else
            {
                if (existingVm is not null)
                {
                    // Remove from list and dispose
                    Profiles.SafeRemove(existingVm);
                    existingVm.Dispose();
                }
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;

            base.Dispose();
        }
    }
}
