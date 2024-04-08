using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class AboutPageViewModel : BaseViewModel
    {
        public string Manufacturer => IDevice.GetCurrent().ManufacturerName;
        public string ProductName => IDevice.GetCurrent().ProductName;
        public string Version => MainWindow.fileVersionInfo.FileVersion!;

        public string InternalSensor => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor)
                ? IDevice.GetCurrent().InternalSensorName
                : "N/A";
        public string ExternalSensor => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor)
                ? IDevice.GetCurrent().ExternalSensorName
                : "N/A";

        public bool IsUnsupportedDevice => IDevice.GetCurrent() is DefaultDevice;

        public BitmapImage DeviceImage => new(new Uri($"pack://application:,,,/Resources/{IDevice.GetCurrent().ProductIllustration}.png"));

        public AboutPageViewModel()
        {
            DeviceManager.UsbDeviceArrived += GenericDeviceUpdated;
            DeviceManager.UsbDeviceRemoved += GenericDeviceUpdated;
        }

        public override void Dispose()
        {
            DeviceManager.UsbDeviceArrived -= GenericDeviceUpdated;
            DeviceManager.UsbDeviceRemoved -= GenericDeviceUpdated;
            base.Dispose();
        }

        private void GenericDeviceUpdated(PnPDevice device, DeviceEventArgs obj)
        {
            // Update all bindings
            OnPropertyChanged(string.Empty);
        }
    }
}
