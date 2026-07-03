using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Pages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class LayoutPageViewModel : BaseViewModel
    {
        private ObservableCollection<LayoutTemplateViewModel> layoutList = [];
        public ListCollectionView LayoutCollectionView { get; set; }

        public LayoutPageViewModel(LayoutPage layoutPage)
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(layoutList, _collectionLock);

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

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            // manage events
            ControllerManager.Initialized += ControllerManager_Initialized;
            VirtualManager.Initialized += VirtualManager_Initialized;

            // raise events
            if (ControllerManager.IsInitialized)
                ControllerManager_Initialized();
            if (VirtualManager.IsInitialized)
                VirtualManager_Initialized();
        }

        private void VirtualManager_Initialized()
        {
            // manage events
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

            // raise events
            VirtualManager_ControllerSelected(VirtualManager.HIDmode);
        }

        private void ControllerManager_Initialized()
        {
            // manage events
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;

            // raise events
            if (ControllerManager.HasTargetController && ControllerManager.GetTarget() is IController controller)
                ControllerManager_ControllerSelected(controller);
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void QuerySettings()
        {
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
            SettingsManager_SettingValueChanged("LayoutFilterOnDevice", ManagerFactory.settingsManager.GetString("LayoutFilterOnDevice"), false, false);
            RefreshLayoutList();
        }

        private void SettingsManager_SettingValueChanged(string? name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "LayoutFilterOnDevice":
                    RefreshLayoutList();
                    break;
            }
        }

        public BitmapImage Artwork
        {
            get
            {
                switch (VirtualManager.HIDmode)
                {
                    default:
                    case HIDmode.Xbox360Controller:
                        return LibraryResources.Xbox360Big;
                    case HIDmode.SwitchProController:
                        return LibraryResources.SwitchProBig;
                    case HIDmode.SteamController:
                        return LibraryResources.SteamControllerBig;
                    case HIDmode.DualShock4Controller:
                        return LibraryResources.DualShock4Big;
                    case HIDmode.DualSenseController:
                        return LibraryResources.DualSenseBig;
                    case HIDmode.SteamDeckController:
                        return LibraryResources.SteamDeckBig;
                }
            }
        }

        private string _layoutName = string.Empty;
        public string LayoutName
        {
            get => _layoutName;
            set
            {
                if (_layoutName != value)
                {
                    _layoutName = value;
                    OnPropertyChanged(nameof(LayoutName));
                }
            }
        }

        private string _layoutDescription = string.Empty;
        public string LayoutDescription
        {
            get => _layoutDescription;
            set
            {
                if (_layoutDescription != value)
                {
                    _layoutDescription = value;
                    OnPropertyChanged(nameof(LayoutDescription));
                }
            }
        }

        private string _layoutAuthor = string.Empty;
        public string LayoutAuthor
        {
            get => _layoutAuthor;
            set
            {
                if (_layoutAuthor != value)
                {
                    _layoutAuthor = value;
                    OnPropertyChanged(nameof(LayoutAuthor));
                }
            }
        }

        // Export-specific properties map to the layout properties
        public string ExportTitle
        {
            get => LayoutName;
            set => LayoutName = value;
        }

        public string ExportDescription
        {
            get => LayoutDescription;
            set => LayoutDescription = value;
        }

        public string ExportAuthor
        {
            get => LayoutAuthor;
            set => LayoutAuthor = value;
        }

        private void VirtualManager_ControllerSelected(HIDmode mode)
        {
            OnPropertyChanged(nameof(Artwork));
        }

        private void ControllerManager_ControllerSelected(IController? controller)
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
            ManagerFactory.layoutManager.Updated += LayoutManager_Updated;
            foreach (LayoutTemplate template in LayoutManager.Templates)
                LayoutManager_Updated(template);
        }

        private void LayoutManager_Updated(LayoutTemplate layoutTemplate)
        {
            lock (_collectionLock)
            {
                LayoutTemplateViewModel? foundPreset = layoutList.FirstOrDefault(p => p.Guid == layoutTemplate.Guid);
                if (foundPreset is not null)
                {
                    int index = layoutList.IndexOf(foundPreset);
                    layoutList[index] = new(layoutTemplate);
                }
                else
                {
                    layoutList.Insert(0, new(layoutTemplate));
                }
            }

            RefreshLayoutList();
        }

        private void RefreshLayoutList()
        {
            // Get filter settings
            bool FilterOnDevice = ManagerFactory.settingsManager.GetBoolean("LayoutFilterOnDevice");

            // Get current controller
            IController? controller = ControllerManager.GetTarget();

            lock (_collectionLock)
            {
                foreach (LayoutTemplateViewModel layoutTemplate in layoutList)
                {
                    if (layoutTemplate.DeviceName is not null && FilterOnDevice)
                    {
                        if (layoutTemplate.DeviceName != controller?.GetType().Name)
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
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // manage events
                ManagerFactory.layoutManager.Updated -= LayoutManager_Updated;
                ManagerFactory.layoutManager.Initialized -= LayoutManager_Initialized;
                ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
                ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
                ControllerManager.Initialized -= ControllerManager_Initialized;
                ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
                VirtualManager.Initialized -= VirtualManager_Initialized;
                VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;
            }

            base.Dispose(disposing);
        }
    }
}
