﻿using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Views.QuickPages;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class QuickProfilesPageViewModel : BaseViewModel
    {
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_ACTIVATION_QP;
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];
        public ObservableCollection<ProfilesPickerViewModel> ProfilePickerItems { get; } = [];

        private QuickProfilesPage quickProfilesPage;
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
                if (value != _selectedPresetIndexDC && value >= 0 && value < ProfilePickerItems.Count)
                {
                    _selectedPresetIndexDC = value;
                    SelectedPresetDC = PowerProfileManager.GetProfile(ProfilePickerItems[_selectedPresetIndexDC].LinkedPresetId.Value);
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
                if (value != _selectedPresetIndexAC && value >= 0 && value < ProfilePickerItems.Count)
                {
                    _selectedPresetIndexAC = value;
                    SelectedPresetAC = PowerProfileManager.GetProfile(ProfilePickerItems[_selectedPresetIndexAC].LinkedPresetId.Value);
                }
            }
        }

        public QuickProfilesPageViewModel(QuickProfilesPage quickProfilesPage)
        {
            this.quickProfilesPage = quickProfilesPage;

            HotkeysManager.Updated += HotkeysManager_Updated;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
            PowerProfileManager.Updated += PowerProfileManager_Updated;
            PowerProfileManager.Deleted += PowerProfileManager_Deleted;

            _devicePresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_DevicePresets };
            _userPresetsPickerVM = new() { IsHeader = true, Text = Resources.PowerProfilesPage_UserPresets };

            ProfilePickerItems.Add(_devicePresetsPickerVM);
            ProfilePickerItems.Add(_userPresetsPickerVM);

            // Fill initial data
            foreach (var preset in PowerProfileManager.profiles.Values)
            {
                var index = ProfilePickerItems.IndexOf(preset.IsDefault() ? _devicePresetsPickerVM : _userPresetsPickerVM) + 1;
                ProfilePickerItems.Insert(index, new ProfilesPickerViewModel { Text = preset.Name, LinkedPresetId = preset.Guid });
            }

            SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == PowerProfileManager.GetDefault().Guid));
            SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == PowerProfileManager.GetDefault().Guid));
        }

        private void PowerProfileManager_Deleted(PowerProfile profile)
        {
            ProfilesPickerViewModel? foundPreset = ProfilePickerItems.FirstOrDefault(p => p.LinkedPresetId == profile.Guid);
            if (foundPreset is not null)
            {
                ProfilePickerItems.Remove(foundPreset);

                if (SelectedPresetAC.Guid == foundPreset.LinkedPresetId)
                    SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == PowerProfileManager.GetDefault().Guid));
                if (SelectedPresetDC.Guid == foundPreset.LinkedPresetId)
                    SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == PowerProfileManager.GetDefault().Guid));
            }
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
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

        public void PowerProfileChanged(PowerProfile powerProfileAC, PowerProfile powerProfileDC)
        {
            SelectedPresetIndexAC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileAC.Guid));
            SelectedPresetIndexDC = ProfilePickerItems.IndexOf(ProfilePickerItems.FirstOrDefault(a => a.LinkedPresetId == powerProfileDC.Guid));
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.ButtonFlags != gyroButtonFlags)
                return;

            HotkeyViewModel? foundHotkey = HotkeysList.ToList().FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is null)
                HotkeysList.SafeAdd(new HotkeyViewModel(hotkey));
            else
                foundHotkey.Hotkey = hotkey;
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            if (hotkeyViewModel != null)
                hotkeyViewModel.SetListening(true, chordTarget);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            if (hotkeyViewModel != null)
                hotkeyViewModel.SetListening(false, storedChord.chordTarget);
        }
    }
}
