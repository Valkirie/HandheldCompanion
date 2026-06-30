using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using System;
using System.Windows.Media.Imaging;

namespace HandheldCompanion.ViewModels
{
    public class AboutPageViewModel : BaseViewModel
    {
        public string Manufacturer => IDevice.GetCurrent().ManufacturerName;
        public string ProductName => IDevice.GetCurrent().ProductName;
        public string Version => App.CurrentVersion.ToString();

        public string InternalSensor => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.InternalSensor)
                ? IDevice.GetCurrent().InternalSensorName
                : "N/A";
        public string ExternalSensor => IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.ExternalSensor)
                ? IDevice.GetCurrent().ExternalSensorName
                : "N/A";

        public bool IsUnsupportedDevice => IDevice.GetCurrent() is DefaultDevice;

        public BitmapImage DeviceImage
        {
            get
            {
                Uri uri = new Uri($"pack://application:,,,/Resources/DeviceImages/{IDevice.GetCurrent().ProductIllustration}.png");
                return new(uri);
            }
        }

        public AboutPageViewModel()
        {
            IDevice.GetCurrent().CapabilitiesChanged += OnCapabilitiesChanged;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IDevice.GetCurrent().CapabilitiesChanged -= OnCapabilitiesChanged;
            }

            base.Dispose(disposing);
        }

        private void OnCapabilitiesChanged(IDevice sender, DeviceCapabilities capabilities)
        {
            UIHelper.TryBeginInvoke(() => OnPropertyChanged(string.Empty));
        }
    }
}
