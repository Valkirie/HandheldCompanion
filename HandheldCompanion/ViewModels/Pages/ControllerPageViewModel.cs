using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Pages;
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
        private ControllerPage controllerPage;

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

        public ControllerPageViewModel(ControllerPage controllerPage)
        {
            this.controllerPage = controllerPage;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(PhysicalControllers, new object());
            BindingOperations.EnableCollectionSynchronization(VirtualControllers, new object());

            // manage events
            ControllerManager.ControllerPlugged += ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerUnplugged;
            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

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

            // send events
            if (ControllerManager.HasTargetController)
                ControllerManager_ControllerSelected(ControllerManager.GetTarget());

            ScanHardwareCommand = new DelegateCommand(async () =>
            {
                // set flag
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

                // force (re)scan
                ControllerManager.Rescan();

                // set flag
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

        private void LayoutManager_Initialized()
        {
            QueryLayouts();
        }

        private object lockcollection = new();
        private void ControllerPlugged(IController Controller, bool IsPowerCycling)
        {
            lock (lockcollection)
            {
                ObservableCollection<ControllerViewModel> controllers = Controller.IsVirtual() ? VirtualControllers : PhysicalControllers;
                ControllerViewModel? foundController = controllers.FirstOrDefault(controller => controller.Controller.GetInstanceId() == Controller.GetInstanceId());
                if (foundController is null)
                {
                    controllers.SafeAdd(new ControllerViewModel(Controller));
                }
                else
                {
                    foundController.Controller = Controller;
                }

                controllerPage.ControllerRefresh();
            }
        }


        private void ControllerUnplugged(IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            lock (lockcollection)
            {
                ObservableCollection<ControllerViewModel> controllers = Controller.IsVirtual() ? VirtualControllers : PhysicalControllers;
                ControllerViewModel? foundController = controllers.FirstOrDefault(controller => controller.Controller.GetInstanceId() == Controller.GetInstanceId());
                if (foundController is not null && !IsPowerCycling)
                {
                    controllers.SafeRemove(foundController);
                    foundController.Dispose();
                }
                else if (foundController is null)
                {
                    LogManager.LogError("Couldn't find ControllerViewModel associated with {0}", Controller.ToString());
                }

                // do something
                controllerPage.ControllerRefresh();
            }
        }

        private void ControllerManager_ControllerSelected(IController Controller)
        {
            lock (lockcollection)
            {
                foreach (ControllerViewModel controller in PhysicalControllers)
                    controller.Updated();
            }

            // check rumble
            CanRumble = Controller.Capabilities.HasFlag(ControllerCapabilities.Rumble);

            // do something
            controllerPage.ControllerRefresh();
        }

        public override void Dispose()
        {
            // manage events
            ControllerManager.ControllerPlugged -= ControllerPlugged;
            ControllerManager.ControllerUnplugged -= ControllerUnplugged;
            ControllerManager.ControllerSelected -= ControllerManager_ControllerSelected;
            ManagerFactory.layoutManager.Initialized -= LayoutManager_Initialized;
            VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;

            base.Dispose();
        }
    }
}
