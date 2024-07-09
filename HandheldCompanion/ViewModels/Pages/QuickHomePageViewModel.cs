using HandheldCompanion.Controls;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HandheldCompanion.ViewModels.Commands;
using WpfScreenHelper.Enum;
using System.Reflection;

namespace HandheldCompanion.ViewModels
{
    public class QuickHomePageViewModel : BaseViewModel
    {
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        public QuickHomePageViewModel()
        {
            HotkeysManager.Updated += HotkeysManager_Updated;
            HotkeysManager.Deleted += HotkeysManager_Deleted;
            HotkeysManager.Initialized += HotkeysManager_Initialized;
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
                if (hotkey.IsPinned)
                    HotkeysList.SafeAdd(new HotkeyViewModel(hotkey));
            }
            else
            {
                if (hotkey.IsPinned)
                    foundHotkey.Hotkey = hotkey;
                else
                    HotkeysManager_Deleted(hotkey);
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
    }
}
