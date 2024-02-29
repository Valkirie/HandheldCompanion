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
        public string Manufacturer => MainWindow.CurrentDevice.ManufacturerName;
        public string ProductName => MainWindow.CurrentDevice.ProductName;
        public string Version => MainWindow.fileVersionInfo.FileVersion!;

        public string InternalSensor => MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.InternalSensor)
                ? MainWindow.CurrentDevice.InternalSensorName
                : "N/A";
        public string ExternalSensor => MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.ExternalSensor)
                ? MainWindow.CurrentDevice.ExternalSensorName
                : "N/A";

        public bool IsUnsupportedDevice => MainWindow.CurrentDevice is DefaultDevice;

        public BitmapImage DeviceImage => new(new Uri($"pack://application:,,,/Resources/{MainWindow.CurrentDevice.ProductIllustration}.png"));

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
