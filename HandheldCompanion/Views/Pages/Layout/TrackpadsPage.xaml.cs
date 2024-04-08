using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages;

public partial class TrackpadsPage : ILayoutPage
{
    public TrackpadsPage()
    {
        DataContext = new TrackpadsPageViewModel();
        InitializeComponent();
    }
}