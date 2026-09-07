using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public sealed record LibraryScanTarget(string Target, string Name);

    public partial class LibraryPageViewModel : BaseViewModel
    {
        private const string AllGamesNavigationKey = "all-games";
        private const string FavoritesNavigationKey = "favorites";
        private const string CollectionsNavigationKey = "collections";
        private const int CollectionPreviewImageCount = 4;
        private const int RecentGamesCount = 10;

        private readonly LibraryNavigationItemViewModel _navL2 = new("nav-l2", "\u21B2");
        private readonly LibraryNavigationItemViewModel _navR2 = new("nav-r2", "\u21B3");

        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = [];
        public ObservableCollection<ProfileViewModel> RecentGames { get; } = [];
        public bool HasRecentGames => RecentGames.Count > 0;
        public ListCollectionView ProfilesView { get; }
        public ItemsPanelTemplate ProfilesCardsItemsPanel { get; } = CreateProfilesCardsItemsPanel();

        public int GameCount => Profiles.Count;

        public string LastLibraryCheckText => GetLastLibraryCheckText();

        private object? _profilesCardsItemsSource;
        public object? ProfilesCardsItemsSource
        {
            get => _profilesCardsItemsSource;
            private set => SetProperty(ref _profilesCardsItemsSource, value);
        }

        public ObservableCollection<CollectionGroupViewModel> CollectionGroups { get; } = [];
        public ObservableCollection<LibraryNavigationItemViewModel> NavigationItems { get; } = [];
        private readonly Dictionary<string, LibraryNavigationItemViewModel> collectionNavigationItems = [];
        private volatile bool _rebuildCollectionGroupsPending;
        private bool _collectionGroupsDirty = true;
        private string? _lastCollectionsOverviewItemKey;

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

        private int _viewMode => ManagerFactory.settingsManager.GetInt("LibraryViewMode");
        public int ViewMode
        {
            get => _viewMode;
            set
            {
                int currentValue = ManagerFactory.settingsManager.GetInt("LibraryViewMode");
                if (value != currentValue)
                {
                    ManagerFactory.settingsManager.SetProperty("LibraryViewMode", value);
                    OnPropertyChanged(nameof(ViewMode));
                    OnPropertyChanged(nameof(IsGridView));
                    OnPropertyChanged(nameof(IsListView));
                    OnPropertyChanged(nameof(IsWideView));
                    OnPropertyChanged(nameof(ShowProfilesCards));
                    OnPropertyChanged(nameof(ShowProfilesList));
                    OnPropertyChanged(nameof(ShowGroupedProfilesList));
                    OnPropertyChanged(nameof(ShowCollectionsOverview));
                    EnsureCollectionGroupsReady();
                }
            }
        }

        public bool IsGridView => ViewMode == 0;
        public bool IsListView => ViewMode == 1;
        public bool IsWideView => ViewMode == 2;
        public bool ShowProfilesCards => !IsCollectionsOverviewSelected && !IsListView;
        public bool ShowProfilesList => !IsCollectionsOverviewSelected && IsListView && IsSingleCollectionSelection;
        public bool ShowGroupedProfilesList => !IsCollectionsOverviewSelected && IsListView && !IsSingleCollectionSelection;
        public bool ShowCollectionsOverview => IsCollectionsOverviewSelected;
        public bool IsSingleCollectionSelection => SelectedNavigationItem?.Kind == LibraryNavigationItemKind.Collection;
        private bool ShouldShowCollectionGroups => ShowCollectionsOverview || ShowGroupedProfilesList;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    UpdateFiltering();
                }
            }
        }

        public bool HasLiked => Profiles.Any(p => p.IsLiked);
        public IReadOnlyCollection<GamePlatform> AvailablePlatforms => Profiles
            .Select(profile => profile.PlatformType)
            .Where(platform => platform != GamePlatform.Generic)
            .Distinct()
            .ToHashSet();

        public ICommand ToggleSortCommand { get; }
        public ICommand ToggleViewModeCommand { get; }
        public ICommand RefreshMetadataCommand { get; }
        public ICommand ScanLibraryCommand { get; }

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

        private BitmapImage _Artwork = null!;
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

        private bool _isInitializing = true;
        public bool IsInitializing
        {
            get => _isInitializing;
            private set
            {
                if (_isInitializing != value)
                {
                    _isInitializing = value;
                    OnPropertyChanged(nameof(IsInitializing));
                }
            }
        }

        private readonly SynchronizationContext _uiContext;

        public event Action? Initialized;

        public LibraryPageViewModel()
        {
            _uiContext = SynchronizationContext.Current!;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Profiles, _collectionLock);

            ProfilesView = new ListCollectionView(Profiles)
            {
                IsLiveSorting = true,
                IsLiveFiltering = true,
                Filter = o => o is ProfileViewModel vm && MatchesFilters(vm)
            };

            ProfilesCardsItemsSource = ProfilesView;

            RebuildNavigationItems();

            ToggleSortCommand = new DelegateCommand(() =>
            {
                SortAscending = !SortAscending;
            });

            ToggleViewModeCommand = new DelegateCommand(() =>
            {
                switch (ViewMode)
                {
                    case 0:
                        ViewMode = 1;
                        break;
                    case 1:
                        ViewMode = 2;
                        break;
                    case 2:
                        ViewMode = 0;
                        break;
                }
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
                        await ManagerFactory.libraryManager.RefreshProfilesArts();
                        break;
                    default:
                        break;
                }
            });

            ScanLibraryCommand = CreateScanLibraryCommand();

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

            // raise events
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

            switch (ManagerFactory.platformManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    PlatformManager_Initialized();
                    break;
            }

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();
        }

        private void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                ControllerManager_ControllerSelected(controller);
        }

        private void PlatformManager_Initialized()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                foreach (LibraryNavigationItemViewModel item in NavigationItems)
                    item.RefreshPlatformGlyph();
            });
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
                _uiContext.Post(_ => RefreshMetadataCommand.Execute(null), null);
            }

            // raise events
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void LibraryManager_NetworkAvailabilityChanged(bool status)
        {
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void ProfileCollectionHelper_CollectionAdded(GameCollection collection)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                // Add the collection navigation item.
                string key = $"collection:{collection.Id}";
                if (!collectionNavigationItems.ContainsKey(key))
                {
                    collectionNavigationItems[key] = new LibraryNavigationItemViewModel(key, collection.Name, collection.Id)
                    {
                        IsVisible = HasProfilesForCollection(collection.Id)
                    };
                    OnPropertyChanged(nameof(NavigationItems));
                }

                // Add the collection group when it already contains profiles.
                if (ShouldShowCollectionGroups && !CollectionGroups.Any(group => group.Collection?.Id == collection.Id))
                {
                    CollectionGroupViewModel group = new(collection, OpenCollection);
                    foreach (ProfileViewModel profile in Profiles.Where(profile => profile.Profile.Collections.Contains(collection.Id)))
                        group.Profiles.Add(profile);

                    if (group.Profiles.Count > 0)
                    {
                        group.SetPreviewProfiles(group.Profiles.Take(CollectionPreviewImageCount));
                        CollectionGroups.Add(group);
                    }
                }
            });
        }

        private void ProfileCollectionHelper_CollectionRemoved(GameCollection collection)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                // Remove the collection navigation item.
                collectionNavigationItems.Remove($"collection:{collection.Id}");
                if (SelectedNavigationItem?.CollectionId == collection.Id)
                    SelectedNavigationItem = FindNavigationItemByKey(AllGamesNavigationKey);
                OnPropertyChanged(nameof(NavigationItems));

                // Remove the collection group.
                CollectionGroupViewModel? group = CollectionGroups.FirstOrDefault(candidate => candidate.Collection?.Id == collection.Id);
                if (group is not null)
                    CollectionGroups.Remove(group);
            });
        }

        private void ProfileCollectionHelper_CollectionUpdated(GameCollection collection)
        {
            UIHelper.TryBeginInvoke(() =>
            {
                CollectionGroupViewModel? group = CollectionGroups.FirstOrDefault(g => g.Collection?.Id == collection.Id);
                group?.RefreshName();
                if (collectionNavigationItems.TryGetValue($"collection:{collection.Id}", out LibraryNavigationItemViewModel? item))
                    item.RefreshTitle(collection.Name);
            });
        }

        private void ControllerManager_ControllerSelected(IController? controller)
        {
            if (controller is null)
                return;

            UIHelper.TryBeginInvoke(() =>
            {
                _navL2.UpdateTriggerGlyph(controller.GetGlyph(AxisFlags.L2));
                _navR2.UpdateTriggerGlyph(controller.GetGlyph(AxisFlags.R2));
            });
        }

    }
}
