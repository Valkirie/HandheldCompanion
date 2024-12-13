using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class HotkeyPageViewModel : BaseViewModel
    {
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];
        public ICommand CreateHotkeyCommand { get; private set; }

        public bool Rumble
        {
            get
            {
                return SettingsManager.GetBoolean("HotkeyRumbleOnExecution");
            }
            set
            {
                SettingsManager.SetProperty("HotkeyRumbleOnExecution", value);
                OnPropertyChanged(nameof(Rumble));
            }
        }

        public HotkeyPageViewModel()
        {
            // manage events
            HotkeysManager.Updated += HotkeysManager_Updated;
            HotkeysManager.Deleted += HotkeysManager_Deleted;
            HotkeysManager.Initialized += HotkeysManager_Initialized;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // raise event
            if (ControllerManager.HasTargetController)
            {
                ControllerManager_ControllerSelected(ControllerManager.GetTargetController());
            }

            if (HotkeysManager.IsInitialized)
            {
                HotkeysManager_Initialized();
            }

            CreateHotkeyCommand = new DelegateCommand(async () =>
            {
                HotkeysManager.UpdateOrCreateHotkey(new Hotkey());
            });
        }

        private void ControllerManager_ControllerSelected(Controllers.IController Controller)
        {
            // (re)draw chords on controller update
            foreach (HotkeyViewModel hotkeyViewModel in HotkeysList)
                hotkeyViewModel.DrawChords();
        }

        private void HotkeysManager_Initialized()
        {
            foreach (Hotkey hotkey in HotkeysManager.GetHotkeys())
                HotkeysManager_Updated(hotkey);
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.IsInternal)
                return;

            HotkeyViewModel? foundHotkey = HotkeysList.ToList().FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is null)
            {
                HotkeysList.SafeAdd(new HotkeyViewModel(hotkey));
            }
            else
            {
                foundHotkey.Hotkey = hotkey;
            }
        }

        private void HotkeysManager_Deleted(Hotkey hotkey)
        {
            HotkeyViewModel? foundHotkey = HotkeysList.ToList().FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is not null)
            {
                HotkeysList.SafeRemove(foundHotkey);
                foundHotkey.Dispose();
            }
        }

        private void InputsManager_StartedListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            hotkeyViewModel?.SetListening(true, chordTarget);
        }

        private void InputsManager_StoppedListening(ButtonFlags buttonFlags, InputsChord storedChord)
        {
            HotkeyViewModel hotkeyViewModel = HotkeysList.Where(h => h.Hotkey.ButtonFlags == buttonFlags).FirstOrDefault();
            hotkeyViewModel?.SetListening(false, storedChord.chordTarget);
        }

        public override void Dispose()
        {
            // manage events
            HotkeysManager.Updated -= HotkeysManager_Updated;
            HotkeysManager.Deleted -= HotkeysManager_Deleted;
            HotkeysManager.Initialized -= HotkeysManager_Initialized;
            InputsManager.StartedListening -= InputsManager_StartedListening;
            InputsManager.StoppedListening -= InputsManager_StoppedListening;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;

            base.Dispose();
        }
    }
}