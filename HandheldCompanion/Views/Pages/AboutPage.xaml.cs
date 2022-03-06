using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Page = System.Windows.Controls.Page;
using System.Windows.Controls;

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

        private HandheldDevice handheldDevice;

        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(string Tag, MainWindow mainWindow, ILogger microsoftLogger) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = mainWindow.pipeClient;

            this.handheldDevice = mainWindow.handheldDevice;

            VersionValue.Text = mainWindow.fileVersionInfo.FileVersion;
            SensorName.Text = handheldDevice.sensorName;
            GyrometerValue.Text = handheldDevice.hasGyrometer ? "Detected" : "N/A";
            AccelerometerValue.Text = handheldDevice.hasAccelerometer ? "Detected" : "N/A";
            InclinometerValue.Text = handheldDevice.hasInclinometer ? "Detected" : "N/A";

            /* List of supported sensors
                - BMI160
            */

            WarningBorder.Visibility = handheldDevice.sensorSupported ? Visibility.Collapsed : Visibility.Visible;
            if (!handheldDevice.sensorSupported)
                WarningContent.Text = "Oups, it appears your device is not compatible.";

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
            /* List of supported devices
                - AYANEO 2021 Pro Retro Power
                - AYANEO 2021 Pro
                - AYANEO 2021
                - AYANEO NEXT Pro
                - AYANEO NEXT Advance
                - AYANEO NEXT
            */

            // Device visual
            Uri ImageSource;

            switch (handheldDevice.ProductName)
            {
                case "AYANEO 2021 Pro Retro Power":
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_aya_retro_power.png");
                    break;
                case "AYANEO 2021 Pro":
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_aya_2021_pro.png");
                    break;
                case "AYANEO 2021":
                    ImageSource = new Uri($"pack://application:,,,/Resources/device_aya_2021.png");
                    break;
                case "AYANEO NEXT Pro":
                case "AYANEO NEXT Advance":
                case "AYANEO NEXT":
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
            var sInfo = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute = true};
            System.Diagnostics.Process.Start(sInfo);

            e.Handled = true;
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }
    }
}
