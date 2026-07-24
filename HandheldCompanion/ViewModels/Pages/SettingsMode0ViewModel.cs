using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class SettingsMode0ViewModel : BaseViewModel
    {
        private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_AIMING;
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        public SettingsMode0ViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(HotkeysList, _collectionLock);

            ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.ButtonFlags != gyroButtonFlags)
                return;

            if (!Monitor.TryEnter(_collectionLock, TimeSpan.FromSeconds(2)))
                return;

            try
            {
                HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
                if (foundHotkey is null)
                    HotkeysList.Add(new HotkeyViewModel(hotkey));
                else
                    foundHotkey.Hotkey = hotkey;
            }
            finally
            {
                Monitor.Exit(_collectionLock);
            }
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(true, chordTarget);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            HotkeyViewModel? hotkeyViewModel = HotkeysList.FirstOrDefault(h => h.Hotkey.ButtonFlags == buttonFlags);
            hotkeyViewModel?.SetListening(false, storedChord.chordTarget);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
                InputsManager.StartedListening -= InputsManager_StartedListening;
                InputsManager.StoppedListening -= InputsManager_StoppedListening;
            }

            base.Dispose(disposing);
        }
    }
}
