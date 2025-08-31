using GongSolutions.Wpf.DragDrop;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class QuickHomePageViewModel : BaseViewModel, IDropTarget
    {
        public ObservableCollection<HotkeyViewModel> HotkeysList { get; set; } = [];

        public QuickHomePageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(HotkeysList, new object());

            // manage events
            ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;
            ManagerFactory.hotkeysManager.Deleted += HotkeysManager_Deleted;

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
                            ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(HotkeysList[i].Hotkey);
                        }
                    }
                }
            }
        }

        private void HotkeysManager_Updated(Hotkey hotkey)
        {
            if (hotkey.IsInternal)
                return;

            HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is null)
            {
                if (hotkey.IsPinned)
                    HotkeysList.SafeInsert(hotkey.PinIndex, new HotkeyViewModel(hotkey));
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
            HotkeyViewModel? foundHotkey = HotkeysList.FirstOrDefault(p => p.Hotkey.ButtonFlags == hotkey.ButtonFlags);
            if (foundHotkey is not null)
            {
                HotkeysList.SafeRemove(foundHotkey);
                foundHotkey.Dispose();
            }
        }
    }
}
