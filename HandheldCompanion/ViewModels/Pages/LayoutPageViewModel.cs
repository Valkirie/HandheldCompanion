using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Views.Pages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class LayoutPageViewModel : BaseViewModel
    {
        private ObservableCollection<LayoutTemplateViewModel> layoutList = [];
        public ListCollectionView LayoutCollectionView { get; set; }

        public LayoutPageViewModel(LayoutPage layoutPage)
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(layoutList, new object());

            // manage events
            ManagerFactory.layoutManager.Updated += LayoutManager_Updated;
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            LayoutCollectionView = new ListCollectionView(layoutList);
            LayoutCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Header"));

            // raise events
            switch (ManagerFactory.layoutManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.layoutManager.Initialized += LayoutManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryLayouts();
                    break;
            }

            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());
        }

        private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                switch (name)
                {
                    case "LayoutFilterOnDevice":
                        RefreshLayoutList();
                        break;
                }
            });
        }

        private void ControllerManager_ControllerSelected(IController controller)
        {
            RefreshLayoutList();
        }

        private void LayoutManager_Initialized()
        {
            QueryLayouts();
            RefreshLayoutList();
        }

        private void QueryLayouts()
        {
            foreach (LayoutTemplate template in LayoutManager.Templates)
                LayoutManager_Updated(template);
        }

        private void LayoutManager_Updated(LayoutTemplate layoutTemplate)
        {
            lock (lockcollection)
            {
                int index;
                LayoutTemplateViewModel? foundPreset = layoutList.FirstOrDefault(p => p.Guid == layoutTemplate.Guid);
                if (foundPreset is not null)
                {
                    index = layoutList.IndexOf(foundPreset);
                    foundPreset = new(layoutTemplate);
                }
                else
                {
                    layoutList.Insert(0, new(layoutTemplate));
                }
            }

            RefreshLayoutList();
        }

        private object lockcollection = new();
        private void RefreshLayoutList()
        {
            // Get filter settings
            bool FilterOnDevice = ManagerFactory.settingsManager.GetBoolean("LayoutFilterOnDevice");

            // Get current controller
            IController? controller = ControllerManager.GetTarget();

            lock (lockcollection)
            {
                foreach (LayoutTemplateViewModel layoutTemplate in layoutList)
                {
                    if (layoutTemplate.ControllerType is not null && FilterOnDevice)
                    {
                        if (layoutTemplate.ControllerType != controller?.GetType())
                        {
                            layoutTemplate.Visibility = Visibility.Collapsed;
                            continue;
                        }
                    }

                    layoutTemplate.Visibility = Visibility.Visible;
                }
            }
        }

        public override void Dispose()
        {
            // manage events
            ManagerFactory.layoutManager.Updated -= LayoutManager_Updated;
            ManagerFactory.layoutManager.Initialized -= LayoutManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;

            base.Dispose();
        }
    }
}
