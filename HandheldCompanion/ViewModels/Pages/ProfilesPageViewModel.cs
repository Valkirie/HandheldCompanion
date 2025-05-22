﻿using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Libraries;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
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
using static HandheldCompanion.Misc.ProcessEx;

namespace HandheldCompanion.ViewModels
{
    public class ProfilesPageViewModel : BaseViewModel
    {
        private ObservableCollection<ProfilesPickerViewModel> _profilePickerItems = [];
        public ListCollectionView ProfilePickerCollectionViewAC { get; set; }
        public ListCollectionView ProfilePickerCollectionViewDC { get; set; }

        public ObservableCollection<LibraryEntryViewModel> LibraryPickers { get; } = [];
        public ObservableCollection<ProcessWindowViewModel> ProfileWindows { get; } = [];

        private ProfilesPage profilesPage;

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

                    // get the power profile view model
                    ProfilesPickerViewModel profilesPickerViewModel = _profilePickerItems.First(p => p.LinkedPresetId == _selectedPresetDC.Guid);
                    _selectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(profilesPickerViewModel);

                    // page-specific behaviors
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
                if (value != _selectedPresetIndexDC && value >= 0 && value < ProfilePickerCollectionViewDC.Count)
                {
                    _selectedPresetIndexDC = value;

                    // get the power profile view model
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePickerCollectionViewDC.GetItemAt(_selectedPresetIndexDC) as ProfilesPickerViewModel;
                    SelectedPresetDC = ManagerFactory.powerProfileManager.GetProfile(profilesPickerViewModel.LinkedPresetId.Value);

                    // refresh property
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

                    // get the power profile view model
                    ProfilesPickerViewModel profilesPickerViewModel = _profilePickerItems.First(p => p.LinkedPresetId == _selectedPresetAC.Guid);
                    _selectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(profilesPickerViewModel);

                    // page-specific behaviors
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
                if (value != _selectedPresetIndexAC && value >= 0 && value < ProfilePickerCollectionViewAC.Count)
                {
                    _selectedPresetIndexAC = value;

                    // get the power profile view model
                    ProfilesPickerViewModel profilesPickerViewModel = ProfilePickerCollectionViewAC.GetItemAt(_selectedPresetIndexAC) as ProfilesPickerViewModel;
                    SelectedPresetAC = ManagerFactory.powerProfileManager.GetProfile(profilesPickerViewModel.LinkedPresetId.Value);

                    // refresh property
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

        public ICommand RefreshLibrary { get; private set; }
        public ICommand DisplayLibrary { get; private set; }
        public ICommand DownloadLibrary { get; private set; }
        public ICommand LaunchExecutable { get; private set; }

        private ContentDialog contentDialog;

        public ProfilesPageViewModel(ProfilesPage profilesPage)
        {
            this.profilesPage = profilesPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(_profilePickerItems, new object());
            BindingOperations.EnableCollectionSynchronization(LibraryPickers, new object());

            ProfilePickerCollectionViewDC = new ListCollectionView(_profilePickerItems);
            ProfilePickerCollectionViewDC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));
            ProfilePickerCollectionViewAC = new ListCollectionView(_profilePickerItems);
            ProfilePickerCollectionViewAC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

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

            switch (ManagerFactory.processManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryForeground();
                    break;
            }

            LaunchExecutable = new DelegateCommand(async () =>
            {
                // dirty, fix me !
                ProfileViewModel profileViewModel = new(ProfilesPage.selectedProfile, false);
                profileViewModel.StartProcessCommand.Execute(null);
            });

            DisplayLibrary = new DelegateCommand(async () =>
            {
                // capture dialog content
                ContentDialog storedDialog = profilesPage.IGGBDialog;
                object content = storedDialog.Content;

                contentDialog = new ContentDialog
                {
                    Title = storedDialog.Title,
                    CloseButtonText = storedDialog.CloseButtonText,
                    PrimaryButtonText = storedDialog.PrimaryButtonText,
                    IsEnabled = storedDialog.IsEnabled,
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

                    if (ProfilesPage.selectedProfile.LibraryEntry is not null && entries.Contains(ProfilesPage.selectedProfile.LibraryEntry))
                        SelectedLibraryEntry = ProfilesPage.selectedProfile.LibraryEntry;
                    else
                        SelectedLibraryEntry = ManagerFactory.libraryManager.GetGame(entries, LibrarySearchField);
                }
            });

            DownloadLibrary = new DelegateCommand(async () =>
            {
                // download arts
                await ManagerFactory.libraryManager.UpdateProfileArts(ProfilesPage.selectedProfile, SelectedLibraryEntry, LibraryCoversIndex, LibraryArtworksIndex);

                // hide dialog
                contentDialog?.Hide();
                contentDialog = null;

                // update profile
                ManagerFactory.profileManager.UpdateOrCreateProfile(ProfilesPage.selectedProfile, UpdateSource.LibraryUpdate);
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

        private void ProcessManager_Initialized()
        {
            QueryForeground();
        }

        private void QueryForeground()
        {
            ProcessEx processEx = ProcessManager.GetForegroundProcess();
            if (processEx is null)
                return;

            ProcessFilter filter = ProcessManager.GetFilter(processEx.Executable, processEx.Path);
            ProcessManager_ForegroundChanged(processEx, null, filter);
        }

        private void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessFilter filter)
        {
            // do something
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

            UIHelper.TryInvoke(() =>
            {
                foreach (PowerProfile powerProfile in ManagerFactory.powerProfileManager.profiles.Values)
                    PowerProfileManager_Updated(powerProfile, UpdateSource.Creation);

                SelectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
                SelectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
            });
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
                ProfilesPickerViewModel? foundPreset = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    _profilePickerItems.Remove(foundPreset);

                    if (SelectedPresetAC.Guid == foundPreset.LinkedPresetId)
                        SelectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
                    if (SelectedPresetDC.Guid == foundPreset.LinkedPresetId)
                        SelectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == Guid.Empty));
                }
            }
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
        {
            lock (ProfilePickerLock)
            {
                int index;
                ProfilesPickerViewModel? foundPreset = _profilePickerItems.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
                if (foundPreset is not null)
                {
                    index = _profilePickerItems.IndexOf(foundPreset);
                    foundPreset.Text = profile.Name;
                }
                else
                {
                    index = 0;
                    _profilePickerItems.Insert(index, new() { LinkedPresetId = profile.Guid, Text = profile.Name, IsInternal = profile.IsDefault() || profile.IsDeviceDefault() });
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

        private ProcessEx selectedProcess;
        public bool HasWindows => ProfileWindows.Any();

        public void ProfileChanged(Profile selectedProfile)
        {
            // update library target profile
            LibrarySearchField = ProfilesPage.selectedProfile.Name;

            // clear list
            ClearLibrary();

            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(Artwork));

            // windows management
            ClearWindows();

            selectedProcess = ProcessManager.GetProcesses().Where(process => process.Path.Equals(selectedProfile.Path)).FirstOrDefault();
            if (selectedProcess is not null)
            {
                selectedProcess.WindowAttached += SelectedProcess_WindowAttached;
                selectedProcess.WindowDetached += SelectedProcess_WindowDetached;

                foreach (ProcessWindow processWindow in selectedProcess.ProcessWindows.Values)
                    SelectedProcess_WindowAttached(processWindow);
            }

            OnPropertyChanged(nameof(HasWindows));
        }

        private void SelectedProcess_WindowAttached(ProcessWindow processWindow)
        {
            ProcessWindowViewModel? foundWindow = ProfileWindows.FirstOrDefault(win => win.ProcessWindow.Hwnd == processWindow.Hwnd);
            if (foundWindow is null)
                ProfileWindows.SafeAdd(new ProcessWindowViewModel(processWindow));
            else
                foundWindow.ProcessWindow = processWindow;

            OnPropertyChanged(nameof(HasWindows));
        }

        private void SelectedProcess_WindowDetached(ProcessWindow processWindow)
        {
            ProcessWindowViewModel? foundWindow = ProfileWindows.FirstOrDefault(win => win.ProcessWindow.Hwnd == processWindow.Hwnd);
            if (foundWindow is not null)
            {
                ProfileWindows.SafeRemove(foundWindow);
                foundWindow.Dispose();
            }

            OnPropertyChanged(nameof(HasWindows));
        }

        private void ClearWindows()
        {
            ProfileWindows.SafeClear();

            if (selectedProcess is not null)
            {
                selectedProcess.WindowAttached -= SelectedProcess_WindowAttached;
                selectedProcess.WindowDetached -= SelectedProcess_WindowDetached;
            }
        }

        public void PowerProfileChanged(PowerProfile powerProfileAC, PowerProfile powerProfileDC)
        {
            lock (ProfilePickerLock)
            {
                UIHelper.TryInvoke(() =>
                {
                    SelectedPresetIndexAC = ProfilePickerCollectionViewAC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid));
                    SelectedPresetIndexDC = ProfilePickerCollectionViewDC.IndexOf(_profilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid));
                });
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
