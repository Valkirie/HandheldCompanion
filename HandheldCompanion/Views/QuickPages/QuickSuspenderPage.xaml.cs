using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
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

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickSuspenderPage.xaml
    /// </summary>
    public partial class QuickSuspenderPage : Page
    {
        public QuickSuspenderPage()
        {
            InitializeComponent();

            MainWindow.processManager.ProcessStarted += ProcessStarted;
            MainWindow.processManager.ProcessStopped += ProcessStopped;
        }

        private void ProcessStopped(ProcessEx processEx)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    var element = processEx.GetBorder();
                    if (CurrentProcesses.Children.Contains(element))
                        CurrentProcesses.Children.Remove(element);
                });
            }
            catch (Exception)
            {
            }
        }

        private void ProcessStarted(ProcessEx processEx)
        {
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    processEx.Draw();
                    var element = processEx.GetBorder();

                    if (!CurrentProcesses.Children.Contains(element))
                        CurrentProcesses.Children.Add(element);
                });
            }
            catch (Exception)
            {
                // process might have exited already
            }
        }
    }
}
