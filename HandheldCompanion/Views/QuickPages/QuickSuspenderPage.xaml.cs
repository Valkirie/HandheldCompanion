using HandheldCompanion.Managers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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

            ProcessManager.ProcessStarted += ProcessStarted;
            ProcessManager.ProcessStopped += ProcessStopped;
        }

        private void ProcessStopped(ProcessEx processEx)
        {
            try
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var element = processEx.GetControl();
                    if (CurrentProcesses.Children.Contains(element))
                        CurrentProcesses.Children.Remove(element);
                });
            }
            catch
            {
            }
        }

        private void ProcessStarted(ProcessEx processEx, bool startup)
        {
            try
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    processEx.DrawControl();
                    var element = processEx.GetControl();

                    if (!CurrentProcesses.Children.Contains(element))
                        CurrentProcesses.Children.Add(element);
                });
            }
            catch
            {
                // process might have exited already
            }
        }
    }
}
