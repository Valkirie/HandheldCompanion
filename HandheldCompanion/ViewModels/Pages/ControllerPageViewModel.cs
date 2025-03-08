using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Views.Pages;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    public class ControllerPageViewModel : BaseViewModel
    {
        private ControllerPage controllerPage;

        public bool LayoutManagerReady => ManagerFactory.layoutManager.IsReady;

        public ObservableCollection<ControllerViewModel> PhysicalControllers { get; set; } = [];
        public ObservableCollection<ControllerViewModel> VirtualControllers { get; set; } = [];

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
                ControllerViewModel? foundController = controllers.ToList().FirstOrDefault(controller => controller.Controller.GetInstanceId() == Controller.GetInstanceId());
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

            base.Dispose();
        }
    }
}
