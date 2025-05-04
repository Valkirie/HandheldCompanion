using HandheldCompanion.Extensions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Views.QuickPages;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class QuickProfilesPageViewModel : BaseViewModel
    {
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_ACTIVATION_QP;
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        private ObservableCollection<ProfilesPickerViewModel> _profilePickerItems = [];
        public ListCollectionView ProfilePickerCollectionViewAC { get; set; }
        public ListCollectionView ProfilePickerCollectionViewDC { get; set; }

        private QuickProfilesPage quickProfilesPage;

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
                    quickProfilesPage.PowerProfile_Selected(_selectedPresetDC, false);

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
                    quickProfilesPage.PowerProfile_Selected(_selectedPresetAC, true);

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

        public QuickProfilesPageViewModel(QuickProfilesPage quickProfilesPage)
        {
            this.quickProfilesPage = quickProfilesPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(HotkeysList, new object());
            BindingOperations.EnableCollectionSynchronization(_profilePickerItems, new object());

            ProfilePickerCollectionViewAC = new ListCollectionView(_profilePickerItems);
            ProfilePickerCollectionViewAC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));
            ProfilePickerCollectionViewDC = new ListCollectionView(_profilePickerItems);
            ProfilePickerCollectionViewDC.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

            // manage events
            ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;

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
                    _profilePickerItems.Insert(index, new() { LinkedPresetId = profile.Guid, Text = profile.Name, IsInternal = profile.IsDefault() });
                }
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

        private object HotkeyListLock = new();
        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.ButtonFlags != gyroButtonFlags)
                return;

            lock (HotkeyListLock)
            {
                HotkeyViewModel? foundHotkey = HotkeysList.ToList().FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
                if (foundHotkey is null)
                    HotkeysList.SafeAdd(new HotkeyViewModel(hotkey));
                else
                    foundHotkey.Hotkey = hotkey;
            }
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            lock (HotkeyListLock)
            {
                HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
                hotkeyViewModel?.SetListening(true, chordTarget);
            }
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            lock (HotkeyListLock)
            {
                HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
                hotkeyViewModel?.SetListening(false, storedChord.chordTarget);
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
            InputsManager.StartedListening -= InputsManager_StartedListening;
            InputsManager.StoppedListening -= InputsManager_StoppedListening;
            ManagerFactory.powerProfileManager.Updated -= PowerProfileManager_Updated;
            ManagerFactory.powerProfileManager.Deleted -= PowerProfileManager_Deleted;
            ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

            base.Dispose();
        }
    }
}
