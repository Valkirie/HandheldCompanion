using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static ControllerCommon.Utils.DeviceUtils;
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
            Uri ImageSource = new Uri($"pack://application:,,,/Resources/{MainWindow.handheldDevice.ProductIllustration}.png");

            // threaded call to update UI
            Dispatcher.Invoke(() =>
            {
                // Motherboard properties
                LabelManufacturer.Content = MainWindow.handheldDevice.ManufacturerName;
                LabelProductName.Content = MainWindow.handheldDevice.ProductName;
                HandheldGrid.Visibility = Visibility.Visible;

                VersionValue.Text = MainWindow.fileVersionInfo.FileVersion;

                SensorInternal.Text = MainWindow.handheldDevice.hasSensors[SensorFamily.Windows] ? MainWindow.handheldDevice.InternalSensorName : string.Empty;
                SensorExternal.Text = MainWindow.handheldDevice.hasSensors[SensorFamily.SerialUSBIMU] ? MainWindow.handheldDevice.ExternalSensorName : string.Empty;

                if (!MainWindow.handheldDevice.ProductSupported)
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
