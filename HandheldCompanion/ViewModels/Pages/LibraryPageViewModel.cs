using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Pages;
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
        public ListCollectionView ProfilesView { get; }

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

        private int _sortTarget = 0;
        public int SortTarget
        {
            get => _sortTarget;
            set
            {
                if (value != _sortTarget)
                {
                    _sortTarget = value;
                    OnPropertyChanged(nameof(SortTarget));

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

        private LibraryPage LibraryPage;
        public LibraryPageViewModel(LibraryPage libraryPage)
        {
            this.LibraryPage = libraryPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Profiles, new object());
            ProfilesView = (ListCollectionView)CollectionViewSource.GetDefaultView(Profiles);
            ProfilesView.IsLiveSorting = true;
            ProfilesView.IsLiveGrouping = true;
            ProfilesView.IsLiveFiltering = true;
            UpdateSorting();

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

            switch (SortTarget)
            {
                default:
                case 0:
                    ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.Name), direction));
                    ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.Name));
                    break;
                case 1:
                    ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.DateCreated), direction));
                    ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.DateCreated));
                    break;
                case 2:
                    ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.LastUsed), direction));
                    ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.LastUsed));
                    break;
            }

            // hack to get ICollectionView to comply with ItemsRepeater
            try
            {
                LibraryPage.ProfilesRepeater.ItemsSource = null;
                LibraryPage.ProfilesRepeater.ItemsSource = ProfilesView;
            }
            catch { }
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

            UIHelper.TryInvoke(UpdateSorting);
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

            UIHelper.TryInvoke(UpdateSorting);
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
