using ControllerCommon;
using ControllerCommon.Devices;
using Microsoft.Extensions.Logging;
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
        private MainWindow mainWindow;
        private ILogger microsoftLogger;
        private PipeClient pipeClient;

        private Device handheldDevice;

        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(string Tag, MainWindow mainWindow, ILogger microsoftLogger, Device handheldDevice) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;

            this.handheldDevice = handheldDevice;

            VersionValue.Text = mainWindow.fileVersionInfo.FileVersion;
            SensorName.Text = handheldDevice.sensorName;
            GyrometerValue.Text = handheldDevice.hasGyrometer ? "Detected" : "N/A";
            AccelerometerValue.Text = handheldDevice.hasAccelerometer ? "Detected" : "N/A";
            InclinometerValue.Text = handheldDevice.hasInclinometer ? "Detected" : "N/A";

            if (!handheldDevice.sensorSupported || !handheldDevice.controllerSupported)
            {
                WarningBorder.Visibility = Visibility.Visible;
                WarningContent.Text = "Oups, it appears your device is not supported yet. The software might not run as expected.";
            }

            UpdateDevice();
        }

        private void cB_AccelDetected_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void cB_GyroDetected_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void UpdateDevice()
        {
            // Device visual
            Uri ImageSource;

            // todo: improve me
            switch (handheldDevice.ProductName)
            {
                case "AYANEO 2021 Pro Retro Power":
                case "AYANEO 2021 Pro":
                case "AYANEO 2021":
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_aya_2021.png");
                    break;
                case "NEXT Pro":
                case "NEXT Advance":
                case "NEXT":
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_aya_next.png");
                    break;
                default:
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_generic.png");
                    break;
            }

            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                // Motherboard properties
                LabelManufacturer.Content = handheldDevice.ManufacturerName;
                LabelProductName.Content = handheldDevice.ProductName;

                ImageDevice.Source = new BitmapImage(ImageSource);
            });
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
