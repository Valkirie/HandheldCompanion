using GameLib.Core;
using GameLib.Plugin.BattleNet.Model;
using GameLib.Plugin.EA.Model;
using GameLib.Plugin.Epic.Model;
using GameLib.Plugin.Gog.Model;
using GameLib.Plugin.Origin.Model;
using GameLib.Plugin.Rockstar.Model;
using GameLib.Plugin.Steam.Model;
using GameLib.Plugin.Ubisoft.Model;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Games;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class LibraryPageViewModel : BaseViewModel
    {
        private const string AllGamesNavigationKey = "all-games";
        private const string FavoritesNavigationKey = "favorites";
        private const string CollectionsNavigationKey = "collections";
        private const int CollectionPreviewImageCount = 4;
        private static readonly (GamePlatform Platform, string Title)[] SupportedPlatforms =
        [
            (GamePlatform.BattleNet, "Battle.net"),
            (GamePlatform.EADesktop, "EA"),
            (GamePlatform.Epic, "Epic"),
            (GamePlatform.GOG, "GOG"),
            (GamePlatform.MicrosoftStore, "Microsoft"),
            (GamePlatform.Origin, "Origin"),
            (GamePlatform.RiotGames, "Riot"),
            (GamePlatform.Rockstar, "Rockstar"),
            (GamePlatform.Steam, "Steam"),
            (GamePlatform.UbisoftConnect, "Ubisoft")
        ];

        private readonly LibraryNavigationItemViewModel _navL2 = new("nav-l2", "\u21B2");
        private readonly LibraryNavigationItemViewModel _navR2 = new("nav-r2", "\u21B3");

        public ObservableCollection<ProfileViewModel> Profiles { get; set; } = [];
        public ListCollectionView ProfilesView { get; }
        public ItemsPanelTemplate ProfilesCardsItemsPanel { get; } = CreateProfilesCardsItemsPanel();

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

        private LibraryNavigationItemViewModel? _selectedNavigationItem;
        public LibraryNavigationItemViewModel? SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value))
                {
                    OnPropertyChanged(nameof(NavigationViewSelectedItem));
                    OnPropertyChanged(nameof(IsCollectionsOverviewSelected));
                    OnPropertyChanged(nameof(IsSingleCollectionSelection));
                    OnPropertyChanged(nameof(ShowProfilesCards));
                    OnPropertyChanged(nameof(ShowProfilesList));
                    OnPropertyChanged(nameof(ShowGroupedProfilesList));
                    OnPropertyChanged(nameof(ShowCollectionsOverview));
                    UpdateFiltering();
                    EnsureCollectionGroupsReady();
                    BackAvailabilityChanged?.Invoke(CanGoBack);
                }
            }
        }

        public LibraryNavigationItemViewModel? NavigationViewSelectedItem
        {
            get => SelectedNavigationItem?.CollectionId.HasValue == true
                ? FindNavigationItemByKey(CollectionsNavigationKey)
                : SelectedNavigationItem;
            set
            {
                // Prevent auto-selection of disabled trigger glyphs by the NavigationView
                if (value?.Kind == LibraryNavigationItemKind.TriggerGlyph)
                {
                    OnPropertyChanged(nameof(NavigationViewSelectedItem));
                    return;
                }

                // The getter returns the "Collections" parent item when a specific collection is active,
                // so the navView's TwoWay binding can back-write the parent item when the page re-enters
                // the frame (e.g. after ContentFrame.GoBack()). Guard against that: if the navView
                // reports the collections root as selected but a specific collection is already active,
                // do not override it. Explicit user navigation goes through navView_ItemInvoked →
                // SelectNavigationItemByKey which sets SelectedNavigationItem directly.
                if (value?.Kind == LibraryNavigationItemKind.CollectionsRoot
                    && SelectedNavigationItem?.Kind == LibraryNavigationItemKind.Collection)
                    return;

                SelectedNavigationItem = value;
            }
        }

        public bool IsCollectionsOverviewSelected => SelectedNavigationItem?.Kind == LibraryNavigationItemKind.CollectionsRoot;

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

        private Dictionary<Type, GamePlatform> keyValuePairs = new Dictionary<Type, GamePlatform>()
        {
            { typeof(BattleNetGame), GamePlatform.BattleNet },
            { typeof(EpicGame), GamePlatform.Epic },
            { typeof(GogGame), GamePlatform.GOG },
            { typeof(OriginGame), GamePlatform.Origin },
            { typeof(GameLib.Plugin.RiotGames.Model.Game), GamePlatform.RiotGames },
            { typeof(RockstarGame), GamePlatform.Rockstar },
            { typeof(SteamGame), GamePlatform.Steam },
            { typeof(UbisoftGame), GamePlatform.UbisoftConnect },
            { typeof(EAGame), GamePlatform.EADesktop },
            { typeof(MicrosoftStoreGame), GamePlatform.MicrosoftStore },
        };

        private readonly SynchronizationContext _uiContext;

        public event Action<bool>? BackAvailabilityChanged;
        public event Action? CollectionOpened;
        public event Action? NavigatedBackToCollections;
        public event Action? Initialized;

        public bool CanGoBack => IsSingleCollectionSelection
            && !string.Equals(SelectedNavigationItem?.Key, FavoritesNavigationKey, StringComparison.Ordinal);

        public bool IsCollectionsOverviewNavigationKey(string? key)
        {
            return string.Equals(key, CollectionsNavigationKey, StringComparison.Ordinal);
        }

        public string? GetCollectionsOverviewItemKey(CollectionGroupViewModel? group)
        {
            if (group is null)
                return null;

            if (group.Collection is not null)
                return $"collection:{group.Collection.Id}";

            return string.Equals(group.Name, "Favorites", StringComparison.Ordinal)
                ? FavoritesNavigationKey
                : null;
        }

        public void RememberCollectionsOverviewItem(CollectionGroupViewModel? group)
        {
            string? key = GetCollectionsOverviewItemKey(group);
            if (!string.IsNullOrWhiteSpace(key))
                _lastCollectionsOverviewItemKey = key;
        }

        public string? GetLastCollectionsOverviewItemKey()
        {
            if (string.IsNullOrWhiteSpace(_lastCollectionsOverviewItemKey))
            {
                _lastCollectionsOverviewItemKey = CollectionGroups
                    .Select(GetCollectionsOverviewItemKey)
                    .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key));
            }

            return _lastCollectionsOverviewItemKey;
        }

        private void OpenCollection(CollectionGroupViewModel group)
        {
            RememberCollectionsOverviewItem(group);

            string? collectionKey = GetCollectionsOverviewItemKey(group);
            LibraryNavigationItemViewModel? collectionItem = FindNavigationItemByKey(collectionKey);

            if (collectionItem is not null)
            {
                SelectedNavigationItem = collectionItem;
                CollectionOpened?.Invoke();
            }
        }

        public bool SelectNavigationItemByKey(string? key)
        {
            LibraryNavigationItemViewModel? selectedItem = FindNavigationItemByKey(key);
            if (selectedItem is null || !selectedItem.IsVisible)
                return false;

            SelectedNavigationItem = selectedItem;
            return true;
        }

        public bool TryGoBack()
        {
            if (!CanGoBack)
                return false;

            LibraryNavigationItemViewModel? collectionsItem = FindNavigationItemByKey(CollectionsNavigationKey);
            if (collectionsItem is null)
                return false;

            SelectedNavigationItem = collectionsItem;
            NavigatedBackToCollections?.Invoke();
            return true;
        }

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

            ScanLibraryCommand = new DelegateCommand<object>(async param =>
            {
                Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = string.Format(Properties.Resources.LibraryScanTitle, param),
                    Content = string.Format(Properties.Resources.LibraryScanContent, param),
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
                }.ShowAsync();

                await dialogTask; // sync call

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        {
                            List<IGame> games = new();

                            switch (param)
                            {
                                case "All":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.All));
                                    break;
                                case "BattleNet":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.BattleNet));
                                    break;
                                case "Epic":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.Epic));
                                    break;
                                case "GOG":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.GOG));
                                    break;
                                case "Origin":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.Origin));
                                    break;
                                case "EA Desktop":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.EADesktop));
                                    break;
                                case "Riot":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.RiotGames));
                                    break;
                                case "Rockstar":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.Rockstar));
                                    break;
                                case "Steam":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.Steam));
                                    break;
                                case "Microsoft Store":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.MicrosoftStore));
                                    break;
                                case "Ubisoft":
                                    games.AddRange(PlatformManager.GetGames(GamePlatform.UbisoftConnect));
                                    break;
                            }

                            foreach (IGame game in games)
                            {
                                Profile? profile = null;
                                bool isCreation;

                                // Try to find an existing profile
                                if (game.Executables.Any())
                                {
                                    foreach (string executable in game.Executables)
                                    {
                                        profile = ManagerFactory.profileManager.GetProfileFromPath(executable, true, true);
                                        if (!profile.Default)
                                            break;
                                    }
                                }
                                else
                                {
                                    profile = ManagerFactory.profileManager.GetProfileFromPath(game.Executable, true, true);
                                }

                                // If profile is found and not default, update it. Otherwise, create a new one.
                                if (profile != null && !profile.Default)
                                {
                                    isCreation = false;
                                }
                                else
                                {
                                    isCreation = true;
                                    profile = new Profile(game.Executable);
                                }

                                if (profile is null)
                                    return;

                                // Filter out unwanted executables
                                IEnumerable<string> Executables = game.Executables.Where(exe =>
                                exe.IndexOf("redist", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("crash", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("setup", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("updater", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("cheat", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("editor", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("tool", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("uninst", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("installer", StringComparison.OrdinalIgnoreCase) < 0);

                                if (string.IsNullOrEmpty(profile.Path) && Executables.Any())
                                    profile.Path = Executables.First();

                                // Set common profile properties
                                profile.Name = game.Name;
                                profile.PlatformType = keyValuePairs[game.GetType()];
                                profile.LaunchString = game.LaunchString;
                                profile.Executables = Executables.ToList();

                                ManagerFactory.profileManager.UpdateOrCreateProfile(profile, isCreation ? UpdateSource.Creation : UpdateSource.LibraryUpdate);
                            }
                        }
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

            // raise events
            switch (ManagerFactory.collectionManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.collectionManager.Initialized += CollectionManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryCollections();
                    break;
            }

            // raise events
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
            // Clear cached platform logos so they can be freshly loaded now that platforms are initialized.
            LibraryNavigationItemViewModel.ClearLogoCache();
            RefreshPlatformIcons();
        }

        private void RefreshPlatformIcons()
        {
            UIHelper.TryBeginInvoke(() =>
            {
                foreach (LibraryNavigationItemViewModel item in NavigationItems)
                    item.RefreshIcon();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
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

        private void QueryCollections()
        {
            ManagerFactory.collectionManager.CollectionAdded += CollectionManager_CollectionAdded;
            ManagerFactory.collectionManager.CollectionRemoved += CollectionManager_CollectionRemoved;
            ManagerFactory.collectionManager.CollectionUpdated += CollectionManager_CollectionUpdated;
            RebuildNavigationItems();
            ScheduleRebuildCollectionGroups();
        }

        private void CollectionManager_Initialized()
        {
            QueryCollections();
        }

        private void CollectionManager_CollectionAdded(GameCollection collection)
        {
            UIHelper.TryInvoke(() =>
            {
                RebuildNavigationItems();
                ScheduleRebuildCollectionGroups();
            });
        }

        private void CollectionManager_CollectionRemoved(GameCollection collection)
        {
            UIHelper.TryInvoke(() =>
            {
                RebuildNavigationItems();
                ScheduleRebuildCollectionGroups();
            });
        }

        private void CollectionManager_CollectionUpdated(GameCollection collection)
        {
            UIHelper.TryInvoke(() =>
            {
                CollectionGroupViewModel? group = CollectionGroups.FirstOrDefault(g => g.Collection?.Id == collection.Id);
                group?.RefreshName();
                RebuildNavigationItems();
            });
        }

        private void ControllerManager_ControllerSelected(IController? controller)
        {
            if (controller is null)
                return;

            UIHelper.TryInvoke(() =>
            {
                _navL2.UpdateTriggerGlyph(controller.GetGlyph(AxisFlags.L2));
                _navR2.UpdateTriggerGlyph(controller.GetGlyph(AxisFlags.R2));
            });
        }

        private void RebuildNavigationItems()
        {
            UIHelper.TryInvoke(() =>
            {
                string selectedKey = SelectedNavigationItem?.Key ?? AllGamesNavigationKey;
                HashSet<GamePlatform> availablePlatforms = AvailablePlatforms.ToHashSet();

                if (NavigationItems.Count == 0)
                {
                    NavigationItems.Add(_navL2);
                    NavigationItems.Add(new LibraryNavigationItemViewModel(AllGamesNavigationKey, "All games", LibraryNavigationItemKind.AllGames));
                    NavigationItems.Add(new LibraryNavigationItemViewModel(FavoritesNavigationKey, "Favorites", LibraryNavigationItemKind.Collection));

                    foreach ((GamePlatform platform, string title) in SupportedPlatforms)
                        NavigationItems.Add(new LibraryNavigationItemViewModel($"platform:{platform}", title, platform));

                    NavigationItems.Add(new LibraryNavigationItemViewModel(CollectionsNavigationKey, "Collections", LibraryNavigationItemKind.CollectionsRoot));
                    NavigationItems.Add(_navR2);
                }

                foreach (var item in NavigationItems)
                {
                    if (item.Key == FavoritesNavigationKey)
                        item.IsVisible = HasLiked;
                    else if (item.Kind == LibraryNavigationItemKind.Platform)
                        item.IsVisible = availablePlatforms.Contains(item.Platform);
                }

                var activeCollections = ManagerFactory.collectionManager
                    .GetCollections()
                    .OrderBy(collection => collection.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                collectionNavigationItems.Clear();

                foreach (GameCollection collection in activeCollections)
                {
                    string key = $"collection:{collection.Id}";
                    bool isVisible = HasProfilesForCollection(collection.Id);

                    LibraryNavigationItemViewModel collectionItem = new(key, collection.Name, collection.Id)
                    {
                        IsVisible = isVisible
                    };

                    collectionNavigationItems[key] = collectionItem;
                }

                LibraryNavigationItemViewModel? selectedItem = FindNavigationItemByKey(selectedKey);

                if (selectedItem is null || !selectedItem.IsVisible)
                    selectedItem = NavigationItems.FirstOrDefault(item => item.IsVisible && item.Kind != LibraryNavigationItemKind.TriggerGlyph)
                                   ?? NavigationItems.FirstOrDefault(item => item.Kind != LibraryNavigationItemKind.TriggerGlyph);

                SelectedNavigationItem = selectedItem;

                OnPropertyChanged(nameof(NavigationItems));
                OnPropertyChanged(nameof(AvailablePlatforms));

                BackAvailabilityChanged?.Invoke(CanGoBack);
            });
        }

        public LibraryNavigationItemViewModel? FindNavigationItemByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (LibraryNavigationItemViewModel item in NavigationItems)
            {
                if (item.Key.Equals(key, StringComparison.Ordinal))
                    return item;
            }

            if (collectionNavigationItems.TryGetValue(key, out LibraryNavigationItemViewModel? collectionItem))
                return collectionItem;

            return null;
        }

        private bool HasProfilesForCollection(Guid collectionId)
        {
            return Profiles.Any(profile => profile.Profile.Collections.Contains(collectionId));
        }

        private void ScheduleRebuildCollectionGroups()
        {
            _collectionGroupsDirty = true;

            if (!ShouldShowCollectionGroups)
                return;

            if (_rebuildCollectionGroupsPending)
                return;

            _rebuildCollectionGroupsPending = true;
            UIHelper.TryInvoke(() =>
            {
                _rebuildCollectionGroupsPending = false;
                RebuildCollectionGroups();
            });
        }

        private void EnsureCollectionGroupsReady()
        {
            if (!ShouldShowCollectionGroups || !_collectionGroupsDirty)
                return;

            ScheduleRebuildCollectionGroups();
        }

        private static ItemsPanelTemplate CreateProfilesCardsItemsPanel()
        {
            FrameworkElementFactory factory = new(typeof(JustifiedWrapPanel));
            factory.SetValue(JustifiedWrapPanel.HorizontalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.VerticalSpacingProperty, 6.0);
            factory.SetValue(JustifiedWrapPanel.TargetRowHeightProperty, 300.0d);
            factory.SetValue(JustifiedWrapPanel.ItemAspectRatioProperty, 475.0 / 900.0);

            return new ItemsPanelTemplate(factory);
        }

        private void RefreshProfilesCardsItemsSource()
        {
            UIHelper.TryInvoke(() =>
            {
                ProfilesCardsItemsSource = null;
                ProfilesCardsItemsSource = ProfilesView;
            });
        }

        private void RebuildCollectionGroups()
        {
            _collectionGroupsDirty = false;
            CollectionGroups.Clear();

            // Use the sorted+filtered view so profiles within each group respect the user's chosen sort order
            List<ProfileViewModel> displayProfiles = ProfilesView.Cast<ProfileViewModel>().ToList();

            // Favorites
            var favGroup = new CollectionGroupViewModel("Favorites", OpenCollection);
            foreach (ProfileViewModel pvm in displayProfiles.Where(p => p.IsLiked))
                favGroup.Profiles.Add(pvm);
            if (favGroup.Profiles.Count > 0)
                CollectionGroups.Add(favGroup);

            // User collections — a profile may appear in more than one
            IReadOnlyList<GameCollection> userCollections = ManagerFactory.collectionManager.GetCollections();
            List<CollectionGroupViewModel> pending = [];
            foreach (GameCollection col in userCollections)
            {
                CollectionGroupViewModel group = new(col, OpenCollection);
                foreach (ProfileViewModel pvm in displayProfiles.Where(p => p.Profile.Collections.Contains(col.Id)))
                    group.Profiles.Add(pvm);
                if (group.Profiles.Count > 0)
                    pending.Add(group);
            }
            foreach (CollectionGroupViewModel group in pending.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                CollectionGroups.Add(group);

            // Other: not liked and not in any user collection
            HashSet<Guid> allColIds = userCollections.Select(c => c.Id).ToHashSet();
            CollectionGroupViewModel otherGroup = new("Other", OpenCollection);
            foreach (ProfileViewModel pvm in displayProfiles.Where(p => !p.IsLiked && !p.Profile.Collections.Any(id => allColIds.Contains(id))))
                otherGroup.Profiles.Add(pvm);
            if (otherGroup.Profiles.Count > 0)
                CollectionGroups.Add(otherGroup);

            foreach (CollectionGroupViewModel group in CollectionGroups)
            {
                IEnumerable<ProfileViewModel> previewProfiles = group.Collection is not null
                    ? displayProfiles.Where(profile => profile.Profile.Collections.Contains(group.Collection.Id))
                    : group.Name switch
                    {
                        "Favorites" => displayProfiles.Where(profile => profile.IsLiked),
                        "Other" => displayProfiles.Where(profile => !profile.IsLiked && !profile.Profile.Collections.Any(id => allColIds.Contains(id))),
                        _ => Enumerable.Empty<ProfileViewModel>()
                    };

                group.SetPreviewProfiles(previewProfiles.Take(CollectionPreviewImageCount));
            }

        }

        private void LibraryManager_Initialized()
        {
            QueryLibrary();
        }

        private void LibraryManager_ProfileStatusChanged(Profile profile, ManagerStatus status)
        {
            ProfileViewModel? profileViewModel = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

            profileViewModel?.IsBusy = status.HasFlag(ManagerStatus.Busy);
        }

        private void QueryProfile()
        {
            // manage events
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;
            ManagerFactory.profileManager.Deleted += ProfileManager_Deleted;

            // Bind the repeater to the sorted view BEFORE any profiles arrive so cards can render incrementally rather than all at once after the bulk load completes
            _uiContext.Post(_ => UpdateSorting(), null);

            foreach (Profile profile in ManagerFactory.profileManager.GetProfiles())
            {
                ProfileManager_Updated(profile, UpdateSource.Background, false);

                foreach (Profile subProfile in ManagerFactory.profileManager.GetSubProfilesFromProfile(profile))
                    ProfileManager_Updated(subProfile, UpdateSource.Background, false);
            }

            // Hide the spinner once every card has been dispatched to the UI
            IsInitializing = false;

            RebuildNavigationItems();
            ScheduleRebuildCollectionGroups();
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void UpdateSorting()
        {
            ListSortDirection direction = SortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            ProfilesView.SortDescriptions.Clear();
            ProfilesView.LiveSortingProperties.Clear();

            // Always sort favorites first (descending IsLiked = favorites on top)
            ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.IsLiked), ListSortDirection.Descending));
            ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.IsLiked));

            // Then apply secondary sort based on user selection
            SortDescription secondary;
            string secondaryProperty;
            switch (SortTarget)
            {
                default:
                case 0:
                    secondary = new SortDescription(nameof(ProfileViewModel.Name), direction);
                    secondaryProperty = nameof(ProfileViewModel.Name);
                    break;
                case 1:
                    secondary = new SortDescription(nameof(ProfileViewModel.PlatformType), direction);
                    secondaryProperty = nameof(ProfileViewModel.PlatformType);
                    break;
                case 2:
                    secondary = new SortDescription(nameof(ProfileViewModel.DateCreated), direction);
                    secondaryProperty = nameof(ProfileViewModel.DateCreated);
                    break;
                case 3:
                    secondary = new SortDescription(nameof(ProfileViewModel.LastUsed), direction);
                    secondaryProperty = nameof(ProfileViewModel.LastUsed);
                    break;
            }
            ProfilesView.SortDescriptions.Add(secondary);
            ProfilesView.LiveSortingProperties.Add(secondaryProperty);

            // Workaround for iNKORE ItemsRepeater not observing ICollectionView changes
            RefreshProfilesCardsItemsSource();

            ScheduleRebuildCollectionGroups();

            OnPropertyChanged(nameof(HasLiked));
        }

        private void ProfileManager_Deleted(Profile profile)
        {
            // ignore me
            if (profile.Default)
                return;

            lock (_collectionLock)
            {
                ProfileViewModel? foundProfile = Profiles.FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
                if (foundProfile is not null)
                {
                    Profiles.Remove(foundProfile);
                    foundProfile.Dispose();
                }
            }

            RebuildNavigationItems();
            ScheduleRebuildCollectionGroups();
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // ignore me
            if (profile.Default)
                return;

            bool shouldShow = profile.ShowInLibrary;

            lock (_collectionLock)
            {
                // find based on guid
                ProfileViewModel? existingVm = Profiles.FirstOrDefault(p => p.Profile.Guid == profile.Guid);

                if (shouldShow)
                {
                    if (existingVm is null)
                    {
                        // Not yet in list, add
                        Profiles.Add(new ProfileViewModel(profile, false, true));
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
                        Profiles.Remove(existingVm);
                        existingVm.Dispose();
                    }
                }
            }

            if (!IsInitializing)
            {
                RebuildNavigationItems();
                ScheduleRebuildCollectionGroups();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // manage events
                ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
                ManagerFactory.profileManager.Deleted -= ProfileManager_Deleted;
                ManagerFactory.libraryManager.ProfileStatusChanged -= LibraryManager_ProfileStatusChanged;
                ManagerFactory.libraryManager.NetworkAvailabilityChanged -= LibraryManager_NetworkAvailabilityChanged;
                ManagerFactory.collectionManager.CollectionAdded -= CollectionManager_CollectionAdded;
                ManagerFactory.collectionManager.CollectionRemoved -= CollectionManager_CollectionRemoved;
                ManagerFactory.collectionManager.CollectionUpdated -= CollectionManager_CollectionUpdated;
                ManagerFactory.collectionManager.Initialized -= CollectionManager_Initialized;
            }

            base.Dispose(disposing);
        }

        private void UpdateFiltering()
        {
            UIHelper.TryInvoke(() =>
            {
                ProfilesView.Filter = o => o is ProfileViewModel vm && MatchesFilters(vm);

                // Workaround for iNKORE ItemsRepeater not observing ICollectionView changes
                RefreshProfilesCardsItemsSource();

                ScheduleRebuildCollectionGroups();
            });
        }

        private bool MatchesFilters(ProfileViewModel profile)
        {
            return MatchesSearchFilter(profile) && MatchesNavigationFilter(profile);
        }

        private bool MatchesSearchFilter(ProfileViewModel profile)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return profile.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   profile.Profile.Executable.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesNavigationFilter(ProfileViewModel profile)
        {
            return SelectedNavigationItem?.Kind switch
            {
                null => true,
                LibraryNavigationItemKind.AllGames => true,
                LibraryNavigationItemKind.CollectionsRoot => true,
                _ when string.Equals(SelectedNavigationItem.Key, FavoritesNavigationKey, StringComparison.Ordinal) => profile.IsLiked,
                LibraryNavigationItemKind.Platform => profile.PlatformType == SelectedNavigationItem.Platform,
                LibraryNavigationItemKind.Collection => SelectedNavigationItem.CollectionId.HasValue &&
                                                        profile.Profile.Collections.Contains(SelectedNavigationItem.CollectionId.Value),
                _ => true
            };
        }
    }
}
