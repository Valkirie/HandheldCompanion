using System.Windows;
using System.Windows.Controls;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickSuspenderPage.xaml
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
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (CurrentProcesses.Children.Contains(processEx))
                    CurrentProcesses.Children.Remove(processEx);
            });
        }
        catch
        {
        }
    }

    private void ProcessStarted(ProcessEx processEx, bool OnStartup)
    {
        try
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (!CurrentProcesses.Children.Contains(processEx))
                    CurrentProcesses.Children.Add(processEx);
            });
        }
        catch
        {
            // process might have exited already
        }
    }
}