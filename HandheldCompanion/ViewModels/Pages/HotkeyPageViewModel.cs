using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
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
                return ManagerFactory.settingsManager.GetBoolean("HotkeyRumbleOnExecution");
            }
            set
            {
                ManagerFactory.settingsManager.SetProperty("HotkeyRumbleOnExecution", value);
                OnPropertyChanged(nameof(Rumble));
            }
        }

        public HotkeyPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(HotkeysList, new object());

            // manage events
            ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
            ManagerFactory.hotkeysManager.Deleted += HotkeysManager_Deleted;
            InputsManager.StartedListening += InputsManager_StartedListening;
            InputsManager.StoppedListening += InputsManager_StoppedListening;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // raise event
            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());

            // raise events
            switch (ManagerFactory.hotkeysManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.hotkeysManager.Initialized += HotkeysManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryHotkeys();
                    break;
            }

            CreateHotkeyCommand = new DelegateCommand(async () =>
            {
                ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(new Hotkey());
            });
        }

        private void ControllerManager_ControllerSelected(Controllers.IController Controller)
        {
            // (re)draw chords on controller update
            foreach (HotkeyViewModel hotkeyViewModel in HotkeysList.ToList())
                hotkeyViewModel.DrawChords();
        }

        private void HotkeysManager_Initialized()
        {
            QueryHotkeys();
        }

        private void QueryHotkeys()
        {
            foreach (Hotkey hotkey in ManagerFactory.hotkeysManager.GetHotkeys())
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
            ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
            ManagerFactory.hotkeysManager.Deleted -= HotkeysManager_Deleted;
            ManagerFactory.hotkeysManager.Initialized -= HotkeysManager_Initialized;
            InputsManager.StartedListening -= InputsManager_StartedListening;
            InputsManager.StoppedListening -= InputsManager_StoppedListening;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;

            base.Dispose();
        }
    }
}