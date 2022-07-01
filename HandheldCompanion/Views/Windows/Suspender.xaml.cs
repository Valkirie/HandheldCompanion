using HandheldCompanion.Managers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Suspender.xaml
    /// </summary>
    public partial class Suspender : Window
    {
        // Process vars
        private ProcessManager processManager;

        public Suspender()
        {
            InitializeComponent();
        }

        public Suspender(ProcessManager processManager) : this()
        {
            this.processManager = processManager;
            this.processManager.ProcessStarted += ProcessStarted;
            this.processManager.ProcessStopped += ProcessStopped;
        }

        private void ProcessStopped(ProcessEx processEx)
        {
            this.Dispatcher.Invoke(() =>
            {
                var element = processEx.GetBorder();
                CurrentProcesses.Children.Remove(element);
            });
        }

        private void ProcessStarted(ProcessEx processEx)
        {
            this.Dispatcher.Invoke(() =>
            {
                processEx.Draw();
                var element = processEx.GetBorder();
                CurrentProcesses.Children.Add(element);
            });
        }

        public void UpdateVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                Visibility visibility = Visibility.Visible;
                switch (Visibility)
                {
                    case Visibility.Visible:
                        visibility = Visibility.Collapsed;
                        break;
                    case Visibility.Collapsed:
                    case Visibility.Hidden:
                        visibility = Visibility.Visible;
                        break;
                }
                Visibility = visibility;
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !isClosing;
            this.Visibility = Visibility.Collapsed;
        }

        private bool isClosing;
        public void Close(bool v)
        {
            isClosing = v;
            this.Close();
        }
    }
}
