using HandheldCompanion.Extensions;
using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.ViewModels.Misc;
using HandheldCompanion.Views.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using static HandheldCompanion.Libraries.LibraryEntry;
using static HandheldCompanion.Managers.LibraryManager;

namespace HandheldCompanion.ViewModels
{
    public class ProfilesPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProfilesPickerViewModel> ProfilePickerItems { get; } = [];
        public ObservableCollection<LibraryEntryViewModel> LibraryPickers { get; } = [];

        private ProfilesPage profilesPage;
        private ProfilesPickerViewModel _devicePresetsPickerVM;
        private ProfilesPickerViewModel _userPresetsPickerVM;

        private PowerProfile _selectedPresetDC;
        public PowerProfile SelectedPresetDC
        {
            get => _selectedPresetDC;
            set
            {
                if (_selectedPresetDC != value)
                {
                    // update variable
                    _selectedPresetDC = value;

                    // page-specific behaviors
                    _selectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.First(p => p.LinkedPresetId == _selectedPresetDC.Guid));
                    profilesPage.PowerProfile_Selected(_selectedPresetDC, false);

                    // refresh all properties
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        private int _selectedPresetIndexDC;
        public int SelectedPresetIndexDC
        {
            get => _selectedPresetIndexDC;
            set
            {
                // Ensure the index is within the bounds of the collection
                if (value != _selectedPresetIndexDC && value >= 0 && value < ProfilePickerItems.Count)
                {
                    if (ProfilePickerItems[value].IsHeader)
                        return;

                    _selectedPresetIndexDC = value;

                    SelectedPresetDC = ManagerFactory.powerProfileManager.GetProfile(ProfilePickerItems[_selectedPresetIndexDC].LinkedPresetId.Value);
                    OnPropertyChanged(nameof(SelectedPresetIndexDC));
                }
            }
        }

        private PowerProfile _selectedPresetAC;
        public PowerProfile SelectedPresetAC
        {
            get => _selectedPresetAC;
            set
            {
                if (_selectedPresetAC != value)
                {
                    // update variable
                    _selectedPresetAC = value;

                    // page-specific behaviors
                    _selectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.First(p => p.LinkedPresetId == _selectedPresetAC.Guid));
                    profilesPage.PowerProfile_Selected(_selectedPresetAC, true);

                    // refresh all properties
                    OnPropertyChanged(string.Empty);
                }
            }
        }

        private int _selectedPresetIndexAC;
        public int SelectedPresetIndexAC
        {
            get => _selectedPresetIndexAC;
            set
            {
                // Ensure the index is within the bounds of the collection
                if (value != _selectedPresetIndexAC && value >= 0 && value < ProfilePickerItems.Count)
                {
                    if (ProfilePickerItems[value].IsHeader)
                        return;

                    _selectedPresetIndexAC = value;

                    SelectedPresetAC = ManagerFactory.powerProfileManager.GetProfile(ProfilePickerItems[_selectedPresetIndexAC].LinkedPresetId.Value);
                    OnPropertyChanged(nameof(SelectedPresetIndexAC));
                }
            }
        }

        private string _LibrarySearchField;
        public string LibrarySearchField
        {
            get => _LibrarySearchField;
            set
            {
                if (_LibrarySearchField != value)
                {
                    _LibrarySearchField = value;
                    OnPropertyChanged(nameof(LibrarySearchField));
                }
            }
        }

        public bool HasLibraryEntry => _SelectedLibraryEntry != null;

        private LibraryEntry _SelectedLibraryEntry;
        public LibraryEntry SelectedLibraryEntry
        {
            get => _SelectedLibraryEntry;
            set
            {
                if (_SelectedLibraryEntry != value)
                {
                    // update index
                    _SelectedLibraryEntry = value;

                    if (value != null)
                    {
                        _SelectedLibraryIndex = LibraryPickers.IndexOf(LibraryPickers.FirstOrDefault(p => p.Id == value.Id));
                    }
                    else
                        _SelectedLibraryIndex = -1;
                    OnPropertyChanged(nameof(SelectedLibraryIndex));

                    SelectedLibraryChanged();
                }
            }
        }

        private int _SelectedLibraryIndex;
        public int SelectedLibraryIndex
        {
            get => _SelectedLibraryIndex;
            set
            {
                if (_SelectedLibraryIndex != value)
                {
                    // update index
                    _SelectedLibraryIndex = value;

                    if (value >= 0 && value < LibraryPickers.Count)
                    {
                        _SelectedLibraryEntry = LibraryPickers[value].LibEntry;
                    }
                    else
                        _SelectedLibraryEntry = null;
                    OnPropertyChanged(nameof(SelectedLibraryEntry));

                    SelectedLibraryChanged();
                }
            }
        }

        private int _LibraryCoversIndex;
        public int LibraryCoversIndex
        {
            get => _LibraryCoversIndex;
            set
            {
                // Launch the download without blocking
                if (value != -1)
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.cover | LibraryType.thumbnails);
                else
                    RefreshCover(value);
            }
        }

        public ObservableCollection<LibraryVisualViewModel> LibraryCovers
        {
            get
            {
                if (_SelectedLibraryIndex != -1 && _SelectedLibraryIndex < LibraryPickers.Count)
                    return LibraryPickers[_SelectedLibraryIndex].LibraryCovers;

                return new();
            }
        }

        private int _LibraryArtworksIndex;
        public int LibraryArtworksIndex
        {
            get => _LibraryArtworksIndex;
            set
            {
                // Launch the download without blocking
                if (value != -1)
                    _ = TriggerGameArtDownloadAsync(value, LibraryType.artwork | LibraryType.thumbnails);
                else
                    RefreshArtwork(value);
            }
        }

        public ObservableCollection<LibraryVisualViewModel> LibraryArtworks
        {
            get
            {
                if (_SelectedLibraryIndex != -1 && _SelectedLibraryIndex < LibraryPickers.Count)
                    return LibraryPickers[_SelectedLibraryIndex].LibraryArtworks;

                return new();
            }
        }

        private async Task TriggerGameArtDownloadAsync(int value, LibraryType libraryType)
        {
            if (_SelectedLibraryEntry is not null)
                await ManagerFactory.libraryManager.DownloadGameArt(_SelectedLibraryEntry, value, libraryType);

            if (libraryType.HasFlag(LibraryType.cover))
                RefreshCover(value);
            else
                RefreshArtwork(value);
        }

        private void SelectedLibraryChanged()
        {
            OnPropertyChanged(nameof(HasLibraryEntry));

            // reset artworks
            LibraryArtworksIndex = -1;
            LibraryArtworksIndex = 0;

            // reset cover
            LibraryCoversIndex = -1;
            LibraryCoversIndex = 0;
        }

        private void RefreshCover(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryCovers));
                _LibraryCoversIndex = index;
                OnPropertyChanged(nameof(LibraryCoversIndex));
            }
            catch (Exception ex)
            {
                // dirty, fix me !
            }
        }

        private void RefreshArtwork(int index)
        {
            try
            {
                OnPropertyChanged(nameof(LibraryArtworks));
                _LibraryArtworksIndex = index;
                OnPropertyChanged(nameof(LibraryArtworksIndex));
            }
            catch (Exception ex)
            {
                // dirty, fix me !
            }
        }

        public bool IsLibraryBusy => ManagerFactory.libraryManager.IsBusy;
        public bool IsLibraryConnected => ManagerFactory.libraryManager.IsConnected;

        private Profile libraryProfile;

        public ICommand RefreshLibrary { get; private set; }
        public ICommand DisplayLibrary { get; private set; }
        public ICommand DownloadLibrary { get; private set; }
        public ICommand LaunchExecutable { get; private set; }

        private ContentDialog contentDialog;

        public ProfilesPageViewModel(ProfilesPage profilesPage)
        {
            this.profilesPage = profilesPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ProfilePickerItems, new object());
            BindingOperations.EnableCollectionSynchronization(LibraryPickers, new object());

            // raise events
            switch (ManagerFactory.powerProfileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryPowerProfile();
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

            _devicePresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_DevicePresets };
            _userPresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_UserPresets };

            ProfilePickerItems.Add(_devicePresetsPickerVM);
            ProfilePickerItems.Add(_userPresetsPickerVM);

            LaunchExecutable = new DelegateCommand(async () =>
            {
                // dirty, fix me !
                ProfileViewModel profileViewModel = new(ProfilesPage.selectedProfile, false);
                profileViewModel.StartProcessCommand.Execute(null);
            });

            DisplayLibrary = new DelegateCommand(async () =>
            {
                // capture dialog content
                object content = profilesPage.IGGBDialog.Content;

                contentDialog = new ContentDialog
                {
                    Title = profilesPage.IGGBDialog.Title,
                    CloseButtonText = profilesPage.IGGBDialog.CloseButtonText,
                    IsEnabled = this.IsLibraryConnected,
                    Content = content,
                    DataContext = this,
                };

                contentDialog.ShowAsync();
                RefreshLibrary.Execute(null);
            });

            RefreshLibrary = new DelegateCommand(async () =>
            {
                // clear list
                ClearLibrary();

                // get games
                IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(LibraryFamily.SteamGrid, LibrarySearchField);
                if (entries.Count() != 0)
                {
                    // sort entries
                    entries = entries.OrderBy(entry => entry.Name);

                    foreach (LibraryEntry entry in entries)
                        LibraryPickers.SafeAdd(new(entry));

                    if (libraryProfile.LibraryEntry is not null && entries.Contains(libraryProfile.LibraryEntry))
                        SelectedLibraryEntry = libraryProfile.LibraryEntry;
                    else
                        SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                // download arts
                await ManagerFactory.libraryManager.UpdateProfileArts(libraryProfile, SelectedLibraryEntry, LibraryCoversIndex, LibraryArtworksIndex);

                // hide dialog
                contentDialog?.Hide();

                // update profile
                ManagerFactory.profileManager.UpdateOrCreateProfile(libraryProfile, UpdateSource.LibraryUpdate);
            });
        }

        private void ClearLibrary()
        {
            // clear list
            LibraryArtworksIndex = -1;
            LibraryCoversIndex = -1;
            SelectedLibraryIndex = -1;
            LibraryPickers.SafeClear();
        }

        private void QueryLibrary()
        {
            // manage events
            ManagerFactory.libraryManager.StatusChanged += LibraryManager_StatusChanged;
            ManagerFactory.libraryManager.NetworkAvailabilityChanged += LibraryManager_NetworkAvailabilityChanged;

            // raise events
            OnPropertyChanged(nameof(IsLibraryConnected));
            OnPropertyChanged(nameof(IsLibraryBusy));
        }

        private void LibraryManager_Initialized()
        {
            QueryLibrary();
        }

        private void LibraryManager_NetworkAvailabilityChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(IsLibraryConnected));
        }

        private void LibraryManager_StatusChanged(ManagerStatus status)
        {
            OnPropertyChanged(nameof(IsLibraryBusy));
        }

        private void QueryPowerProfile()
        {
            // manage events
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;

            foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);

            SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
            SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
        }

        private void PowerProfileManager_Initialized()
        {
            QueryPowerProfile();
        }

        private object ProfilePickerLock = new();
        private void PowerProfileManager_Deleted(PowerProfile profile)
        {
            lock (ProfilePickerLock)
            {
                ProfilesPickerViewModel? foundPreset = ProfilePickerItems.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    ProfilePickerItems.Remove(foundPreset);

                    if (SelectedPresetAC.Guid == foundPreset.LinkedPresetId)
                        SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
                    if (SelectedPresetDC.Guid == foundPreset.LinkedPresetId)
                        SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
                }
            }
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
        {
            lock (ProfilePickerLock)
            {
                int index;
                ProfilesPickerViewModel? foundPreset = ProfilePickerItems.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    index = ProfilePickerItems.IndexOf(foundPreset);
                    foundPreset.Text = profile.Name;
                }
                else
                {
                    index = ProfilePickerItems.IndexOf(profile.IsDefault() ? _devicePresetsPickerVM : _userPresetsPickerVM) + 1;
                    ProfilePickerItems.Insert(index, new() { LinkedPresetId = profile.Guid, Text = profile.Name });
                }
            }
        }

        public BitmapImage Cover
        {
            get
            {
                if (ProfilesPage.selectedProfile?.LibraryEntry == null)
                    return LibraryResources.MissingCover;

                long id = ProfilesPage.selectedProfile.LibraryEntry.Id;
                long imageId = ProfilesPage.selectedProfile.LibraryEntry.GetCoverId();
                string imageExtension = ProfilesPage.selectedProfile.LibraryEntry.GetCoverExtension(false);

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryType.cover, imageId, imageExtension);
            }
        }

        public BitmapImage Artwork
        {
            get
            {
                if (ProfilesPage.selectedProfile?.LibraryEntry == null)
                    return null;

                long id = ProfilesPage.selectedProfile.LibraryEntry.Id;
                long imageId = ProfilesPage.selectedProfile.LibraryEntry.GetArtworkId();
                string imageExtension = ProfilesPage.selectedProfile.LibraryEntry.GetArtworkExtension(false);

                BitmapImage artwork = ManagerFactory.libraryManager.GetGameArt(id, LibraryType.artwork, imageId, imageExtension);
                if (artwork != LibraryResources.MissingCover)
                    return artwork;

                return null;
            }
        }

        public void ProfileChanged(Profile selectedProfile)
        {
            // clear list
            ClearLibrary();

            // update library target profile
            libraryProfile = ProfilesPage.selectedProfile.Clone() as Profile;
            LibrarySearchField = libraryProfile.Name;

            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(Artwork));
        }

        public void PowerProfileChanged(PowerProfile powerProfileAC, PowerProfile powerProfileDC)
        {
            lock (ProfilePickerLock)
            {
                SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid));
                SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid));
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.powerProfileManager.Updated -= PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
            ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;
            ManagerFactory.libraryManager.Initialized -= LibraryManager_Initialized;
            ManagerFactory.libraryManager.StatusChanged -= LibraryManager_StatusChanged;
            ManagerFactory.libraryManager.NetworkAvailabilityChanged -= LibraryManager_NetworkAvailabilityChanged;

            base.Dispose();
        }
    }
}
