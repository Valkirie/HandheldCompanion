using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;

namespace HandheldCompanion.ViewModels
{
    public abstract class ILayoutPageViewModel : BaseViewModel
    {
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public ILayoutPageViewModel()
        {
            ControllerManager.ControllerSelected += UpdateController;
            DeviceManager.UsbDeviceArrived += DeviceManager_UsbDeviceUpdated;
            DeviceManager.UsbDeviceRemoved += DeviceManager_UsbDeviceUpdated;
        }

        public override void Dispose()
        {
            ControllerManager.ControllerSelected -= UpdateController;
            DeviceManager.UsbDeviceArrived -= DeviceManager_UsbDeviceUpdated;
            DeviceManager.UsbDeviceRemoved -= DeviceManager_UsbDeviceUpdated;
            base.Dispose();
        }

        private void DeviceManager_UsbDeviceUpdated(PnPDevice device, Guid IntefaceGuid)
        {
            IController controller = ControllerManager.GetTargetController();
            if (controller is not null) UpdateController(controller);
        }

        protected abstract void UpdateController(IController controller);
    }
}
