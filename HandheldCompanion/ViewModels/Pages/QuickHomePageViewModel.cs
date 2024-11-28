using GongSolutions.Wpf.DragDrop;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class QuickHomePageViewModel : BaseViewModel, IDropTarget
    {
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        public QuickHomePageViewModel()
        {
            // manage events
            HotkeysManager.Updated += HotkeysManager_Updated;
            HotkeysManager.Deleted += HotkeysManager_Deleted;
            HotkeysManager.Initialized += HotkeysManager_Initialized;

            if (HotkeysManager.IsInitialized)
            {
                HotkeysManager_Initialized();
            }
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            dropInfo.Effects = System.Windows.DragDropEffects.All;
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is HotkeyViewModel source)
            {
                if (dropInfo.TargetItem is HotkeyViewModel target)
                {
                    int sourceIndex = HotkeysList.IndexOf(source);
                    int targetIndex = HotkeysList.IndexOf(target);

                    if (sourceIndex >= 0 && targetIndex >= 0 && sourceIndex != targetIndex)
                    {
                        // Remove the source item from its original position
                        HotkeysList.RemoveAt(sourceIndex);

                        // Insert the source item at the new target position
                        HotkeysList.Insert(targetIndex, source);

                        // Determine the range of affected items and their new indices
                        int start = Math.Min(sourceIndex, targetIndex);
                        int end = Math.Max(sourceIndex, targetIndex);

                        // Update the PinIndex of each affected item
                        for (int i = start; i <= end; i++)
                        {
                            HotkeysList[i].Hotkey.PinIndex = i;
                            HotkeysManager.UpdateOrCreateHotkey(HotkeysList[i].Hotkey);
                        }
                    }
                }
            }
        }

        private void HotkeysManager_Initialized()
        {
            foreach (Hotkey hotkey in HotkeysManager.GetHotkeys().OrderBy(hotkey => hotkey.PinIndex))
                HotkeysManager_Updated(hotkey);
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.IsInternal)
                return;

            HotkeyViewModel? foundHotkey = HotkeysList.ToList().FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is null)
            {
                HotkeyViewModel hotkeyViewModel = new HotkeyViewModel(hotkey);
                HotkeysList.SafeAdd(hotkeyViewModel);
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
    }
}
