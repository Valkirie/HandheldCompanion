using craftersmine.SteamGridDBNet;
using HandheldCompanion.Helpers;
using HandheldCompanion.Libraries;
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
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using static HandheldCompanion.Libraries.LibraryEntry;

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

        private bool _isUpdatingSelection = false;

        public bool HasLibraryEntry => _SelectedLibraryEntry != null;

        private LibraryEntry _SelectedLibraryEntry;
        public LibraryEntry SelectedLibraryEntry
        {
            get => _SelectedLibraryEntry;
            set
            {
                // Only update if the new value is different.
                if (_SelectedLibraryEntry != value)
                {
                    // Prevent recursive updates.
                    if (_isUpdatingSelection)
                        return;

                    try
                    {
                        _isUpdatingSelection = true;
                        _SelectedLibraryEntry = value;
                        if (value != null)
                            _SelectedLibraryIndex = LibraryPickers.IndexOf(LibraryPickers.First(p => p.Id == value.Id));
                        else
                            _SelectedLibraryIndex = -1;

                        // Notify about both properties.
                        OnPropertyChanged(nameof(SelectedLibraryEntry));
                        OnPropertyChanged(nameof(SelectedLibraryIndex));
                        OnPropertyChanged(nameof(HasLibraryEntry));
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }

        private int _SelectedLibraryIndex;
        public int SelectedLibraryIndex
        {
            get => _SelectedLibraryIndex;
            set
            {
                // Only update if index is different.
                if (_SelectedLibraryIndex != value)
                {
                    // Prevent recursive updates.
                    if (_isUpdatingSelection)
                        return;

                    try
                    {
                        _isUpdatingSelection = true;
                        _SelectedLibraryIndex = value;
                        if (value >= 0 && value < LibraryPickers.Count)
                            _SelectedLibraryEntry = LibraryPickers[value].LibEntry;
                        else
                            _SelectedLibraryEntry = null;

                        // Notify about both properties.
                        OnPropertyChanged(nameof(SelectedLibraryIndex));
                        OnPropertyChanged(nameof(SelectedLibraryEntry));
                        OnPropertyChanged(nameof(HasLibraryEntry));

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

        public bool IsLibraryBusy => ManagerFactory.libraryManager.IsBusy;

        public ICommand RefreshLibrary { get; private set; }
        public ICommand DisplayLibrary { get; private set; }
        public ICommand DownloadLibrary { get; private set; }

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

            DisplayLibrary = new DelegateCommand(async () =>
            {
                // clear list
                LibraryPickers.Clear();
                SelectedLibraryIndex = -1;

                LibrarySearchField = ProfilesPage.selectedProfile.Name;
                await profilesPage.IGGBDialog.ShowAsync();
            });

            RefreshLibrary = new DelegateCommand(async () =>
            {
                LibraryPickers.Clear();

                IEnumerable<LibraryEntry> entries = await ManagerFactory.libraryManager.GetGames(LibraryFamily.SteamGrid, LibrarySearchField);
                if (entries.Count() != 0)
                {
                    foreach (LibraryEntry entry in entries)
                        LibraryPickers.Add(new(entry));

                    SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                // update target entry
                ProfilesPage.selectedProfile.LibraryEntry = SelectedLibraryEntry;

                // download arts
                await ManagerFactory.libraryManager.DownloadGameArts(SelectedLibraryEntry, false);

                // update profile
                ManagerFactory.profileManager.UpdateOrCreateProfile(ProfilesPage.selectedProfile, UpdateSource.ArtUpdateOnly);
            });
        }

        private void LibraryManager_StatusChanged(ManagerStatus status)
        {
            OnPropertyChanged(nameof(IsLibraryBusy));
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

        public BitmapImage Cover
        {
            get
            {
                if (ProfilesPage.selectedProfile?.LibraryEntry == null)
                    return LibraryResources.MissingCover;

                long id = ProfilesPage.selectedProfile.LibraryEntry.Id;
                LibraryFamily libraryFamily = ProfilesPage.selectedProfile.LibraryEntry.Family;

                return ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.cover);
            }
        }

        public BitmapImage Artwork
        {
            get
            {
                if (ProfilesPage.selectedProfile?.LibraryEntry == null)
                    return null;

                long id = ProfilesPage.selectedProfile.LibraryEntry.Id;
                LibraryFamily libraryFamily = ProfilesPage.selectedProfile.LibraryEntry.Family;

                BitmapImage artwork = ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.artwork);
                if (artwork != LibraryResources.MissingCover)
                    return artwork;

                BitmapImage screenshot = ManagerFactory.libraryManager.GetGameArt(id, LibraryManager.LibraryType.screenshot);
                if (screenshot != LibraryResources.MissingCover)
                    return screenshot;

                return null;
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
