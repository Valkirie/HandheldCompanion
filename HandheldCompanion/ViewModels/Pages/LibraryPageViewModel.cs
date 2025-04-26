using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
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

        private bool _sortAscending => ManagerFactory.settingsManager.GetBoolean("LibrarySortAscending");
        public bool SortAscending
        {
            get => _sortAscending;
            set
            {
                if (value != SortAscending)
                {
                    ManagerFactory.settingsManager.SetProperty("LibrarySortAscending", value);
                    OnPropertyChanged(nameof(SortAscending));

                    UpdateSorting();
                }
            }
        }

        private int _sortTarget => ManagerFactory.settingsManager.GetInt("LibrarySortTarget");
        public int SortTarget
        {
            get => _sortTarget;
            set
            {
                if (value != _sortTarget)
                {
                    ManagerFactory.settingsManager.SetProperty("LibrarySortTarget", value);
                    OnPropertyChanged(nameof(SortTarget));

                    UpdateSorting();
                }
            }
        }

        public ICommand ToggleSortCommand { get; }
        public ICommand RefreshMetadataCommand { get; }

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

        public bool IsLibraryConnected => ManagerFactory.libraryManager.IsConnected;

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

            RefreshMetadataCommand = new DelegateCommand(async () =>
            {
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = Properties.Resources.LibraryDiscoverTitle,
                    Content = Properties.Resources.LibraryDiscoverContent,
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        ManagerFactory.libraryManager.RefreshProfilesArts();
                        break;
                    default:
                        break;
                }
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

            switch (ManagerFactory.libraryManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.libraryManager.Initialized += LibraryManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryLibrary();
                    break;
            }
        }

        private void QueryLibrary()
        {
            // manage events
            ManagerFactory.libraryManager.ProfileStatusChanged += LibraryManager_ProfileStatusChanged;
            ManagerFactory.libraryManager.NetworkAvailabilityChanged += LibraryManager_NetworkAvailabilityChanged;

            // get latest known version
            Version LastVersion = Version.Parse(ManagerFactory.settingsManager.GetString("LastVersion"));
            if (LastVersion < Version.Parse(Settings.VersionLibraryManager))
            {
                UIHelper.TryInvoke(() => { RefreshMetadataCommand.Execute(null); });
            }

            // raise events
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void LibraryManager_NetworkAvailabilityChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void LibraryManager_Initialized()
        {
            QueryLibrary();
        }

        private void LibraryManager_ProfileStatusChanged(Profile profile, ManagerStatus status)
        {
            ProfileViewModel? profileViewModel = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

            if (profileViewModel is not null)
                profileViewModel.IsBusy = status.HasFlag(ManagerStatus.Busy);
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
            ManagerFactory.libraryManager.ProfileStatusChanged -= LibraryManager_ProfileStatusChanged;

            base.Dispose();
        }
    }
}
