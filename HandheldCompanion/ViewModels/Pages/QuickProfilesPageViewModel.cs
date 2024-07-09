using HandheldCompanion.Controls;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HandheldCompanion.ViewModels.Commands;
using WpfScreenHelper.Enum;
using System.Reflection;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.ViewModels
{
    public class QuickProfilesPageViewModel : BaseViewModel
    {
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_ACTIVATION_QP;
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        public QuickProfilesPageViewModel()
        {
            HotkeysManager.Updated += HotkeysManager_Updated;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
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

        private void InputsManager_StartedListening(ButtonFlags buttonFlags)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            if (hotkeyViewModel != null)
                hotkeyViewModel.SetListening(true);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            if (hotkeyViewModel != null)
                hotkeyViewModel.SetListening(false);
        }
    }
}
