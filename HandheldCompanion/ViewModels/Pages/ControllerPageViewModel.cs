using HandheldCompanion.Controllers;
using System;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using static HandheldCompanion.Managers.ControllerManager;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class ControllerPageViewModel : BaseViewModel
    {
        public bool LayoutManagerReady => ManagerFactory.layoutManager.IsReady;

        private bool _CanRumble;
        public bool CanRumble
        {
            get
            {
                return _CanRumble;
            }
            set
            {
                if (_CanRumble != value)
                {
                    _CanRumble = value;
                    OnPropertyChanged(nameof(CanRumble));
                }
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
                    case HIDmode.DualShock4Controller:
                        return LibraryResources.DualShock4Big;
                }
            }
        }

        public ObservableCollection<ControllerViewModel> PhysicalControllers { get; set; } = [];
        public ObservableCollection<ControllerViewModel> VirtualControllers { get; set; } = [];
        public ICommand ScanHardwareCommand { get; private set; }
        public ICommand OpenWindowsControl { get; private set; }
        public ICommand NavigateSettings { get; private set; }

        private Visibility _ScanHardwareVisibility = Visibility.Collapsed;
        public Visibility ScanHardwareVisibility
        {
            get => _ScanHardwareVisibility;
            set
            {
                if (value != _ScanHardwareVisibility)
                {
                    _ScanHardwareVisibility = value;
                    OnPropertyChanged(nameof(ScanHardwareVisibility));
                }
            }
        }

        private Visibility _PhysicalDevicesVisibility = Visibility.Collapsed;
        public Visibility PhysicalDevicesVisibility
        {
            get => _PhysicalDevicesVisibility;
            set
            {
                if (value != _PhysicalDevicesVisibility)
                {
                    _PhysicalDevicesVisibility = value;
                    OnPropertyChanged(nameof(PhysicalDevicesVisibility));
                }
            }
        }

        private Visibility _WarningNoPhysicalVisibility = Visibility.Collapsed;
        public Visibility WarningNoPhysicalVisibility
        {
            get => _WarningNoPhysicalVisibility;
            set
            {
                if (value != _WarningNoPhysicalVisibility)
                {
                    _WarningNoPhysicalVisibility = value;
                    OnPropertyChanged(nameof(WarningNoPhysicalVisibility));
                }
            }
        }

        private Visibility _VirtualDevicesVisibility = Visibility.Collapsed;
        public Visibility VirtualDevicesVisibility
        {
            get => _VirtualDevicesVisibility;
            set
            {
                if (value != _VirtualDevicesVisibility)
                {
                    _VirtualDevicesVisibility = value;
                    OnPropertyChanged(nameof(VirtualDevicesVisibility));
                }
            }
        }

        private Visibility _WarningNoVirtualVisibility = Visibility.Collapsed;
        public Visibility WarningNoVirtualVisibility
        {
            get => _WarningNoVirtualVisibility;
            set
            {
                if (value != _WarningNoVirtualVisibility)
                {
                    _WarningNoVirtualVisibility = value;
                    OnPropertyChanged(nameof(WarningNoVirtualVisibility));
                }
            }
        }

        private Visibility _HintsNotMutedVisibility = Visibility.Collapsed;
        public Visibility HintsNotMutedVisibility
        {
            get => _HintsNotMutedVisibility;
            set
            {
                if (value != _HintsNotMutedVisibility)
                {
                    _HintsNotMutedVisibility = value;
                    OnPropertyChanged(nameof(HintsNotMutedVisibility));
                }
            }
        }

        private bool _ControllerSettingsEnabled = true;
        public bool ControllerSettingsEnabled
        {
            get => _ControllerSettingsEnabled;
            set
            {
                if (_ControllerSettingsEnabled != value)
                {
                    _ControllerSettingsEnabled = value;
                    OnPropertyChanged(nameof(ControllerSettingsEnabled));
                    OnPropertyChanged(nameof(ScanHardwareCardEnabled));
                }
            }
        }

        public bool ScanHardwareCardEnabled => ControllerSettingsEnabled && !_isScanning;

        private Visibility _SlotFixNowVisibility = Visibility.Collapsed;
        public Visibility SlotFixNowVisibility
        {
            get => _SlotFixNowVisibility;
            set
            {
                if (value != _SlotFixNowVisibility)
                {
                    _SlotFixNowVisibility = value;
                    OnPropertyChanged(nameof(SlotFixNowVisibility));
                }
            }
        }

        private Visibility _WarningVirtualNotSlot1Visibility = Visibility.Collapsed;
        public Visibility WarningVirtualNotSlot1Visibility
        {
            get => _WarningVirtualNotSlot1Visibility;
            set
            {
                if (value != _WarningVirtualNotSlot1Visibility)
                {
                    _WarningVirtualNotSlot1Visibility = value;
                    OnPropertyChanged(nameof(WarningVirtualNotSlot1Visibility));
                }
            }
        }

        private int _slotManagementModeIndex;
        public int ControllerSlotManagementModeIndex
        {
            get => _slotManagementModeIndex;
            set
            {
                if (_slotManagementModeIndex != value)
                {
                    _slotManagementModeIndex = value;
                    OnPropertyChanged(nameof(ControllerSlotManagementModeIndex));
                    UpdateSlotFixVisibility();
                }
            }
        }

        private bool _hasSlotIssue;
        private bool _virtualNotInSlot1;
        private bool _isScanning;
        private int _hidStatus;

        private Visibility _HIDManagedByProfileVisibility = Visibility.Collapsed;
        public Visibility HIDManagedByProfileVisibility
        {
            get => _HIDManagedByProfileVisibility;
            set
            {
                if (value != _HIDManagedByProfileVisibility)
                {
                    _HIDManagedByProfileVisibility = value;
                    OnPropertyChanged(nameof(HIDManagedByProfileVisibility));
                }
            }
        }

        private bool _HidModeEnabled = true;
        public bool HidModeEnabled
        {
            get => _HidModeEnabled;
            set
            {
                if (_HidModeEnabled != value)
                {
                    _HidModeEnabled = value;
                    OnPropertyChanged(nameof(HidModeEnabled));
                }
            }
        }

        public ControllerPageViewModel()
        {
            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(PhysicalControllers, _collectionLock);
            BindingOperations.EnableCollectionSynchronization(VirtualControllers, _collectionLock2);

            // manage events
            ControllerManager.ControllerPlugged += ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerUnplugged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            ControllerManager.StatusChanged += ControllerManager_StatusChanged;
            ControllerManager.SlotIssueChanged += ControllerManager_SlotIssueChanged;
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

            // initialize slot issue state
            _hasSlotIssue = ControllerManager.HasSlotIssue;
            _virtualNotInSlot1 = ControllerManager.HasVirtualSlot1Issue;

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
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }

            // send events
            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());
            else
                Refresh();

            ScanHardwareCommand = new DelegateCommand(async () =>
            {
                // set flags
                _isScanning = true;
                OnPropertyChanged(nameof(ScanHardwareCardEnabled));
                ScanHardwareVisibility = Visibility.Visible;

                // get all physical controllers
                foreach (IController controller in ControllerManager.GetPhysicalControllers<IController>())
                {
                    // force unplug
                    string devicePath = controller.GetInstanceId();
                    if (ManagerFactory.deviceManager.FindDevice(devicePath) is not null)
                        ControllerManager.Unplug(controller);
                }

                await Task.Delay(2000).ConfigureAwait(false);

                // force (re)scan — run on a background thread; RefreshXInputAsync internally
                // does multiple round-trips with up to 7 s of retries per device, so calling
                // it synchronously here would block the UI for several seconds.
                await Task.Run(ControllerManager.Rescan).ConfigureAwait(false);

                // clear flags
                _isScanning = false;
                OnPropertyChanged(nameof(ScanHardwareCardEnabled));
                ScanHardwareVisibility = Visibility.Collapsed;
            });

            OpenWindowsControl = new DelegateCommand<string>(async (target) =>
            {
                // Full-trust (Win32) component
                Process.Start(new ProcessStartInfo("control.exe", target) { UseShellExecute = true });
            });

            NavigateSettings = new DelegateCommand<string>(async (target) =>
            {
                // Needed on .NET/WPF to invoke URI protocols
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            });
        }

        private void VirtualManager_ControllerSelected(HIDmode mode)
        {
            OnPropertyChanged(nameof(Artwork));
        }

        private void QueryLayouts()
        {
            OnPropertyChanged(nameof(LayoutManagerReady));
        }

        private void QueryProfile()
        {
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
        }

        private void QuerySettings()
        {
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            SettingsManager_SettingValueChanged("HIDstatus", ManagerFactory.settingsManager.GetString("HIDstatus"), false);
            SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false);
            SettingsManager_SettingValueChanged("ControllerSlotManagementMode", ManagerFactory.settingsManager.GetString("ControllerSlotManagementMode"), false);
        }

        private void LayoutManager_Initialized()
        {
            QueryLayouts();
        }

        private void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            bool managedByProfile = !profile.Default && profile.HID != HIDmode.NotSelected;
            HIDManagedByProfileVisibility = managedByProfile ? Visibility.Visible : Visibility.Collapsed;
            HidModeEnabled = !managedByProfile;
        }

        private void ControllerPlugged(IController Controller, bool IsPowerCycling)
        {
            ObservableCollection<ControllerViewModel> controllers = Controller.IsVirtual() ? VirtualControllers : PhysicalControllers;
            object lockObj = Controller.IsVirtual() ? _collectionLock2 : _collectionLock;

            lock (lockObj)
            {
                ControllerViewModel? foundController = controllers.FirstOrDefault(controller => controller.Controller.GetInstanceId() == Controller.GetInstanceId());
                if (foundController is null)
                {
                    controllers.Add(new ControllerViewModel(Controller));
                }
                else
                {
                    foundController.Controller = Controller;
                }
            }

            Refresh();
        }


        private void ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            ObservableCollection<ControllerViewModel> controllers = Controller.IsVirtual() ? VirtualControllers : PhysicalControllers;
            object lockObj = Controller.IsVirtual() ? _collectionLock2 : _collectionLock;

            lock (lockObj)
            {
                ControllerViewModel? foundController = controllers.FirstOrDefault(controller => controller.Controller.GetInstanceId() == Controller.GetInstanceId());
                if (foundController is not null && !IsPowerCycling)
                {
                    controllers.Remove(foundController);
                    foundController.Dispose();
                }
                else if (foundController is null)
                {
                    LogManager.LogError("Couldn't find ControllerViewModel associated with {0}", Controller.ToString());
                }
            }

            // do something
            Refresh();
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            lock (_collectionLock)
            {
                foreach (ControllerViewModel controller in PhysicalControllers)
                    controller.Updated();
            }

            // check rumble
            CanRumble = Controller.Capabilities.HasFlag(ControllerCapabilities.Rumble);

            // do something
            Refresh();
        }

        public void Refresh()
        {
            IController targetController = ControllerManager.GetTarget();

            bool hasPhysical, hasVirtual, hasTarget;
            lock (_collectionLock)
            {
                hasPhysical = PhysicalControllers.Any();
                hasTarget = targetController != null &&
                            PhysicalControllers.Any(c => c.Controller.GetInstanceId() == targetController.GetInstanceId());
            }
            lock (_collectionLock2) { hasVirtual = VirtualControllers.Any(); }

            bool isHidden = hasTarget && targetController!.IsHidden();
            bool isPlugged = hasPhysical && hasTarget;
            bool hasDualInput = isPlugged && !isHidden && hasVirtual;

            PhysicalDevicesVisibility = hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            WarningNoPhysicalVisibility = !hasPhysical ? Visibility.Visible : Visibility.Collapsed;
            VirtualDevicesVisibility = hasVirtual ? Visibility.Visible : Visibility.Collapsed;
            WarningNoVirtualVisibility = hasTarget && !hasVirtual && (_hidStatus != 0 || isHidden) ? Visibility.Visible : Visibility.Collapsed;
            WarningVirtualNotSlot1Visibility = isHidden && hasVirtual && _virtualNotInSlot1 ? Visibility.Visible : Visibility.Collapsed;
            HintsNotMutedVisibility = hasDualInput ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "HIDstatus":
                    if (!int.TryParse(value?.ToString(), out int hidStatus))
                        hidStatus = 1;
                    _hidStatus = hidStatus;
                    Refresh();
                    break;
                case "SteamControllerMode":
                    Refresh();
                    break;
                case "ControllerSlotManagementMode":
                    if (!int.TryParse(value?.ToString(), out int mode))
                        mode = 0;
                    ControllerSlotManagementModeIndex = Math.Max(0, Math.Min(1, mode));
                    break;
            }
        }

        private void ControllerManager_StatusChanged(ControllerManagerStatus status, int attempts)
        {
            bool enabled = status != ControllerManagerStatus.Busy;
            ControllerSettingsEnabled = enabled;
            Refresh();
        }

        private void ControllerManager_SlotIssueChanged(bool hasIssue, string reason)
        {
            _hasSlotIssue = hasIssue;
            _virtualNotInSlot1 = ControllerManager.HasVirtualSlot1Issue;
            UpdateSlotFixVisibility();
            Refresh();
        }

        private void UpdateSlotFixVisibility()
        {
            SlotFixNowVisibility = _slotManagementModeIndex == 0 && _hasSlotIssue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public override void Dispose()
        {
            // manage events
            ControllerManager.ControllerPlugged -= ControllerPlugged;
            ControllerManager.ControllerUnplugged -= ControllerUnplugged;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
            ControllerManager.StatusChanged -= ControllerManager_StatusChanged;
            ControllerManager.SlotIssueChanged -= ControllerManager_SlotIssueChanged;
            ManagerFactory.layoutManager.Initialized -= LayoutManager_Initialized;
            ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            base.Dispose();
        }
    }
}
