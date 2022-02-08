using Microsoft.Extensions.Configuration;
using ModernWpf.Controls;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ControllerHelperWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            var configuration = new ConfigurationBuilder()
                        .AddJsonFile("ControllerHelper.json")
                        .Build();

            var serilogLogger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            var microsoftLogger = new SerilogLoggerFactory(serilogLogger).CreateLogger("ControllerHelper");

            navView.SelectedItem = navView.MenuItems[0];
        }

        private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {

            }
            else
            {
                NavigationViewItem item = args.SelectedItem as NavigationViewItem;
                switch (item.Content)
                {
                    case "Devices":
                        ContentFrame.Navigate(typeof(Devices));
                        break;
                    case "Profiles":
                        ContentFrame.Navigate(typeof(Profiles));
                        break;
                }
            }
        }
    }
}
