using ControllerCommon;
using ModernWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for Devices.xaml
    /// </summary>
    public partial class Devices : Page
    {
        private MainWindow mainWindow;

        public Devices()
        {
            InitializeComponent();
        }

        public Devices(MainWindow mainWindow) : this()
        {
            this.mainWindow = mainWindow;

            foreach (HIDmode mode in ((HIDmode[])Enum.GetValues(typeof(HIDmode))).Where(a => a != HIDmode.None))
                cB_HidMode.Items.Add(Utils.GetDescriptionFromEnumValue(mode));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void ToggleTheme(object sender, RoutedEventArgs e)
        {
            if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark)
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            }
            else
            {
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
        }

        private void cB_HidMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HIDmode mode = (HIDmode)cB_HidMode.SelectedIndex;

            PipeClientSettings settings = new PipeClientSettings("HIDmode", mode);
            mainWindow.pipeClient.SendMessage(settings);

            // update UI icon to match HIDmode
            ImageBrush uniformToFillBrush = new ImageBrush();
            uniformToFillBrush.Stretch = Stretch.None;

            switch (mode)
            {
                case HIDmode.DualShock4Controller:
                    uniformToFillBrush.ImageSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/ps_controller.png"));
                    break;
                case HIDmode.Xbox360Controller:
                    uniformToFillBrush.ImageSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/xbox_controller.png"));
                    break;
                case HIDmode.None:
                    break;
            }

            // Freeze the brush (make it unmodifiable) for performance benefits.
            uniformToFillBrush.Freeze();

            this.Dispatcher.Invoke(() =>
            {
                MainGrid.Background = uniformToFillBrush;
            });
        }

        internal void UpdateServivce(ServiceControllerStatus status, ServiceStartMode mode)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case ServiceControllerStatus.Paused:
                    case ServiceControllerStatus.Stopped:
                        B_ServiceSwitch.IsEnabled = true;
                        //B_ServiceSwitch.Content = "Turn On";

                        cB_HidMode.IsEnabled = false;
                        break;
                    case ServiceControllerStatus.Running:
                        B_ServiceSwitch.IsEnabled = true;
                        //B_ServiceSwitch.Content = "Turn Off";

                        cB_HidMode.IsEnabled = true;
                        break;
                    case ServiceControllerStatus.StartPending:
                    case ServiceControllerStatus.StopPending:
                        break;
                    default:
                        B_ServiceAlert.Visibility = Visibility.Visible;
                        B_ServiceSwitch.IsEnabled = false;
                    break;
                }
            });
        }
    }
}
