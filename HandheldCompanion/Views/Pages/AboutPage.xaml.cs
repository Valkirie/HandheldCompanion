using HandheldCompanion.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace HandheldCompanion.Views.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        Tag = "about";
        DataContext = new AboutPageViewModel();
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        var sInfo = new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true };
        Process.Start(sInfo);

        e.Handled = true;
    }
}