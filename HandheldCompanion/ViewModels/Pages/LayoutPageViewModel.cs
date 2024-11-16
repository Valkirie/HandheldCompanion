using HandheldCompanion.Controllers;
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
        public ObservableCollection<LayoutTemplateViewModel> LayoutList { get; set; } = [];
        private LayoutTemplateViewModel _layoutTemplates;
        private LayoutTemplateViewModel _layoutCommunity;

        public LayoutPageViewModel(LayoutPage layoutPage)
        {
            // manage events
            LayoutManager.Updated += LayoutManager_Updated;
            LayoutManager.Initialized += LayoutManager_Initialized;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(LayoutList, new object());

            _layoutTemplates = new() { IsHeader = true, Name = Resources.LayoutPage_Templates, Guid = new() };
            _layoutCommunity = new() { IsHeader = true, Name = Resources.LayoutPage_Community, Guid = new() };

            LayoutList.Add(_layoutTemplates);
            LayoutList.Add(_layoutCommunity);

            // raise events
            if (LayoutManager.IsInitialized)
            {
                foreach (LayoutTemplate template in LayoutManager.Templates)
                    LayoutManager_Updated(template);
            }

            if (ControllerManager.HasTargetController)
            {
                ControllerManager_ControllerSelected(ControllerManager.GetTargetController());
            }
        }

        private void SettingsManager_SettingValueChanged(string? name, object value, bool temporary)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
            RefreshLayoutList();
        }

        private void LayoutManager_Updated(LayoutTemplate layoutTemplate)
        {
            int index;
            LayoutTemplateViewModel? foundPreset = LayoutList.FirstOrDefault(p => p.Guid == layoutTemplate.Guid);
            if (foundPreset is not null)
            {
                index = LayoutList.IndexOf(foundPreset);
                foundPreset = new(layoutTemplate);
            }
            else
            {
                index = LayoutList.IndexOf(layoutTemplate.IsInternal ? _layoutTemplates : _layoutCommunity) + 1;
                LayoutList.Insert(index, new(layoutTemplate));
            }
        }

        private void RefreshLayoutList()
        {
            // Get filter settings
            bool FilterOnDevice = SettingsManager.GetBoolean("LayoutFilterOnDevice");

            // Get current controller
            IController? controller = ControllerManager.GetTargetController();

            foreach (LayoutTemplateViewModel layoutTemplate in LayoutList)
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
}
