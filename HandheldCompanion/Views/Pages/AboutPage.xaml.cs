using ControllerCommon;
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
        private PipeClient pipeClient;

        public AboutPage()
        {
            InitializeComponent();
        }

        public AboutPage(string Tag, MainWindow mainWindow) : this()
        {
            this.Tag = Tag;
            this.mainWindow = mainWindow;

            this.pipeClient = mainWindow.pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
            pipeClient.ServerMessage -= OnServerMessage;
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_CONTROLLER:
                    PipeServerHandheld handheldDevice = (PipeServerHandheld)message;
                    UpdateDevice(handheldDevice);
                    break;
            }
        }

        private void cB_AccelDetected_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        private void cB_GyroDetected_Checked(object sender, RoutedEventArgs e)
        {
            // do something
        }

        public void UpdateDevice(PipeServerHandheld handheldDevice)
        {
            // Device visual
            Uri ImageSource = new Uri($"pack://application:,,,/Resources/{handheldDevice.ProductIllustration}.png");

            // threaded call to update UI
            this.Dispatcher.Invoke(() =>
            {
                // Motherboard properties
                LabelManufacturer.Content = handheldDevice.ManufacturerName;
                LabelProductName.Content = handheldDevice.ProductName;
                HandheldGrid.Visibility = Visibility.Visible;

                VersionValue.Text = mainWindow.fileVersionInfo.FileVersion;
                SensorInternal.Text = handheldDevice.hasInternal ? handheldDevice.InternalSensorName : "N/A";
                SensorExternal.Text = handheldDevice.hasExternal ? handheldDevice.ExternalSensorName : "N/A";

                if (!handheldDevice.ProductSupported)
                {
                    WarningBorder.Visibility = Visibility.Visible;
                    WarningContent.Text = "Oups, it appears your device is not supported yet. The software might not run as expected.";
                }

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
