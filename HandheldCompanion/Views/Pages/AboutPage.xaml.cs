using ControllerCommon.Devices;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(string Tag) : this()
        {
            this.Tag = Tag;

            // call functions
            UpdateDevice(null);
        }

        public void UpdateDevice(PnPDevice device)
        {
            // Device visual
            Uri ImageSource = new Uri($"pack://application:,,,/Resources/{MainWindow.CurrentDevice.ProductIllustration}.png");

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Motherboard properties
                LabelManufacturer.Text = MainWindow.CurrentDevice.ManufacturerName;
                LabelProductName.Text = MainWindow.CurrentDevice.ProductName;
                HandheldGrid.Visibility = Visibility.Visible;

                VersionValue.Text = MainWindow.fileVersionInfo.FileVersion;

                SensorInternal.Text = MainWindow.CurrentDevice.Capacities.HasFlag(ControllerCommon.Devices.DeviceCapacities.InternalSensor) ? MainWindow.CurrentDevice.InternalSensorName : string.Empty;
                SensorExternal.Text = MainWindow.CurrentDevice.Capacities.HasFlag(ControllerCommon.Devices.DeviceCapacities.ExternalSensor) ? MainWindow.CurrentDevice.ExternalSensorName : string.Empty;

                if (MainWindow.CurrentDevice.GetType() == typeof(DefaultDevice))
                {
                    WarningBorder.Visibility = Visibility.Visible;
                    WarningContent.Text = "Oups, it appears your device is not supported yet. The software might not run as expected.";
                }

                ImageDevice.Source = new BitmapImage(ImageSource);
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void cB_AccelDetected_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void cB_GyroDetected_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var sInfo = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true };
            System.Diagnostics.Process.Start(sInfo);

            e.Handled = true;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }
    }
}
