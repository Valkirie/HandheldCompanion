using GameLib.Core;
using GameLib.Plugin.BattleNet.Model;
using GameLib.Plugin.EA.Model;
using GameLib.Plugin.Epic.Model;
using GameLib.Plugin.Gog.Model;
using GameLib.Plugin.Origin.Model;
using GameLib.Plugin.Rockstar.Model;
using GameLib.Plugin.Steam.Model;
using GameLib.Plugin.Ubisoft.Model;
using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
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

        private Dictionary<Type, PlatformType> keyValuePairs = new Dictionary<Type, PlatformType>()
        {
            { typeof(BattleNetGame), PlatformType.BattleNet },
            { typeof(EpicGame), PlatformType.Epic },
            { typeof(GogGame), PlatformType.GOG },
            { typeof(OriginGame), PlatformType.Origin },
            { typeof(GameLib.Plugin.RiotGames.Model.Game), PlatformType.RiotGames },
            { typeof(RockstarGame), PlatformType.Rockstar },
            { typeof(SteamGame), PlatformType.Steam },
            { typeof(UbisoftGame), PlatformType.UbisoftConnect },
            { typeof(EAGame), PlatformType.EADesktop },
        };

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
                                    games.AddRange(PlatformManager.GetGames());
                                    break;
                                case "BattleNet":
                                    games.AddRange(PlatformManager.BattleNet.GetGames());
                                    break;
                                case "Epic":
                                    games.AddRange(PlatformManager.Epic.GetGames());
                                    break;
                                case "GOG":
                                    games.AddRange(PlatformManager.GOGGalaxy.GetGames());
                                    break;
                                case "Origin":
                                    games.AddRange(PlatformManager.Origin.GetGames());
                                    break;
                                case "EA Desktop":
                                    games.AddRange(PlatformManager.EADesktop.GetGames());
                                    break;
                                case "Riot":
                                    games.AddRange(PlatformManager.RiotGames.GetGames());
                                    break;
                                case "Rockstar":
                                    games.AddRange(PlatformManager.Rockstar.GetGames());
                                    break;
                                case "Steam":
                                    games.AddRange(PlatformManager.Steam.GetGames());
                                    break;
                                case "Ubisoft":
                                    games.AddRange(PlatformManager.UbisoftConnect.GetGames());
                                    break;
                            }

                            foreach (IGame game in games)
                            {
                                // Skip Steamworks Shared for Steam games
                                if (game is SteamGame && game.Id == "228980")
                                    continue;

                                Profile profile = null;
                                bool isCreation;

                                // Try to find an existing profile
                                if (game is SteamGame steamGame)
                                {
                                    foreach (string executable in steamGame.Executables)
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
                                exe.IndexOf("uninst", StringComparison.OrdinalIgnoreCase) < 0 &&
                                exe.IndexOf("installer", StringComparison.OrdinalIgnoreCase) < 0);

                                if (string.IsNullOrEmpty(profile.Path) && Executables.Any())
                                    profile.Path = Executables.First();

                                // Set common profile properties
                                profile.Name = game.Name;
                                profile.PlatformType = keyValuePairs[game.GetType()];
                                profile.LaunchString = game.LaunchString;
                                profile.Executables = Executables.ToList();

                                ManagerFactory.profileManager.UpdateOrCreateProfile(profile, isCreation ? UpdateSource.Creation : UpdateSource.Background);
                                ManagerFactory.libraryManager.RefreshProfileArts(profile, isCreation ? UpdateSource.Creation : UpdateSource.LibraryUpdate);
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

        private void LibraryManager_NetworkAvailabilityChanged(bool status)
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
                case 3:
                    ProfilesView.SortDescriptions.Add(new SortDescription(nameof(ProfileViewModel.PlatformType), direction));
                    ProfilesView.LiveSortingProperties.Add(nameof(ProfileViewModel.PlatformType));
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

            ProfileViewModel? foundProfile = Profiles.FirstOrDefault(p => p.Profile == profile || p.Profile.Guid == profile.Guid);
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
            ManagerFactory.libraryManager.NetworkAvailabilityChanged -= LibraryManager_NetworkAvailabilityChanged;

            base.Dispose();
        }
    }
}
