using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Views.Pages;
using IGDB;
using IGDB.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace HandheldCompanion.ViewModels
{
    public class ProfilesPageViewModel : BaseViewModel
    {
        public ObservableCollection<ProfilesPickerViewModel> ProfilePickerItems { get; } = [];
        public ObservableCollection<GameViewModel> IGDBPickers { get; } = [];

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

        private string _IGDBSearch;
        public string IGDBSearch
        {
            get => _IGDBSearch;
            set
            {
                if (_IGDBSearch != value)
                {
                    _IGDBSearch = value;
                    OnPropertyChanged(nameof(IGDBSearch));
                }
            }
        }

        private bool _isUpdatingSelection = false;

        public bool HasIGDBTarget => _SelectedIGDB != null;

        private Game _SelectedIGDB;
        public Game SelectedIGDB
        {
            get => _SelectedIGDB;
            set
            {
                // Only update if the new value is different.
                if (_SelectedIGDB != value)
                {
                    // Prevent recursive updates.
                    if (_isUpdatingSelection)
                        return;

                    try
                    {
                        _isUpdatingSelection = true;
                        _SelectedIGDB = value;
                        if (value != null)
                            _SelectedIGDBIndex = IGDBPickers.IndexOf(IGDBPickers.First(p => p.Id == value.Id));
                        else
                            _SelectedIGDBIndex = -1;

                        // Notify about both properties.
                        OnPropertyChanged(nameof(SelectedIGDB));
                        OnPropertyChanged(nameof(SelectedIGDBIndex));
                        OnPropertyChanged(nameof(HasIGDBTarget));
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }

        private int _SelectedIGDBIndex;
        public int SelectedIGDBIndex
        {
            get => _SelectedIGDBIndex;
            set
            {
                // Only update if index is different.
                if (_SelectedIGDBIndex != value)
                {
                    // Prevent recursive updates.
                    if (_isUpdatingSelection)
                        return;

                    try
                    {
                        _isUpdatingSelection = true;
                        _SelectedIGDBIndex = value;
                        if (value >= 0 && value < IGDBPickers.Count)
                            _SelectedIGDB = IGDBPickers[value].Game;
                        else
                            _SelectedIGDB = null;

                        // Notify about both properties.
                        OnPropertyChanged(nameof(SelectedIGDBIndex));
                        OnPropertyChanged(nameof(SelectedIGDB));
                        OnPropertyChanged(nameof(HasIGDBTarget));

                        // Optionally trigger game art download here if needed.
                        // ManagerFactory.libraryManager.DownloadGameArts(SelectedIGDB, true);
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }

        public bool IGDBBusy => ManagerFactory.libraryManager.IsBusy;

        public ICommand RefreshIGDB { get; private set; }
        public ICommand DisplayIGDB { get; private set; }
        public ICommand DownloadIGDB { get; private set; }

        public ProfilesPageViewModel(ProfilesPage profilesPage)
        {
            this.profilesPage = profilesPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(ProfilePickerItems, new object());

            // manage events
            ManagerFactory.powerProfileManager.Updated += PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted += PowerProfileManager_Deleted;
            ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
            ManagerFactory.libraryManager.StatusChanged += LibraryManager_StatusChanged;

            _devicePresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_DevicePresets };
            _userPresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_UserPresets };

            ProfilePickerItems.Add(_devicePresetsPickerVM);
            ProfilePickerItems.Add(_userPresetsPickerVM);

            DisplayIGDB = new DelegateCommand(async () =>
            {
                // clear list
                IGDBPickers.Clear();
                SelectedIGDBIndex = -1;

                IGDBSearch = ProfilesPage.selectedProfile.Name;
                await profilesPage.IGGBDialog.ShowAsync();
            });

            RefreshIGDB = new DelegateCommand(async () =>
            {
                IEnumerable<Game> games = await ManagerFactory.libraryManager.GetGames(IGDBSearch);
                if (games.Count() == 0)
                    return;

                IGDBPickers.Clear();
                foreach (Game game in games)
                    IGDBPickers.Add(new(game));

                SelectedIGDB = ManagerFactory.libraryManager.GetGame(games, IGDBSearch);
            });

            DownloadIGDB = new DelegateCommand(async () =>
            {
                // update target IGDB game
                ProfilesPage.selectedProfile.IGDB = SelectedIGDB;

                // download arts
                await ManagerFactory.libraryManager.DownloadGameArts(SelectedIGDB, false);

                // update profile
                ManagerFactory.profileManager.UpdateOrCreateProfile(ProfilesPage.selectedProfile, UpdateSource.ArtUpdateOnly);
            });
        }

        private void LibraryManager_StatusChanged(ManagerStatus status)
        {
            OnPropertyChanged(nameof(IGDBBusy));
        }

        private void PowerProfileManager_Initialized()
        {
            SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
            SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == ManagerFactory.powerProfileManager.GetDefault().Guid));
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

        public ImageBrush Cover
        {
            get
            {
                if (ProfilesPage.selectedProfile?.IGDB?.Id == null)
                    return null;

                long id = (long)ProfilesPage.selectedProfile.IGDB.Id;
                return ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.cover);
            }
        }

        public ImageBrush Artwork
        {
            get
            {
                if (ProfilesPage.selectedProfile?.IGDB?.Id == null)
                    return null;

                long id = (long)ProfilesPage.selectedProfile.IGDB.Id;
                return
                    ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.artwork) ??
                    ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.screenshot) ??
                    ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.cover);
            }
        }

        public void ProfileChanged(Profile selectedProfile)
        {
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
            ManagerFactory.libraryManager.StatusChanged -= LibraryManager_StatusChanged;

            base.Dispose();
        }
    }
}
