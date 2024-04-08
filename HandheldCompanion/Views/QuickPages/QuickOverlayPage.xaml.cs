using HandheldCompanion.ViewModels;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickOverlayPage : Page
{
    public QuickOverlayPage()
    {
        Tag = "quickoverlay";
        DataContext = new OverlayPageViewModel();
        InitializeComponent();
    }
}