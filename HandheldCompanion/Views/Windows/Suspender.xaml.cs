using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using System;
using System.Windows;

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

            this.SourceInitialized += Suspender_SourceInitialized;
        }

        private void Suspender_SourceInitialized(object? sender, EventArgs e)
        {
            this.HideMinimizeAndMaximizeButtons();
        }

        public Suspender(ProcessManager processManager) : this()
        {
            this.processManager = processManager;
            this.processManager.ProcessStarted += ProcessStarted;
            this.processManager.ProcessStopped += ProcessStopped;
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
            catch(Exception)
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
            catch(Exception)
            {
                // process might have exited already
            }
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
                        WindowState = WindowState.Normal;
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
