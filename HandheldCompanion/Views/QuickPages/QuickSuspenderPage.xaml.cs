using HandheldCompanion.Managers;
using System;
using System.Windows.Controls;

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

        private void ProcessStarted(ProcessEx processEx, bool startup)
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
