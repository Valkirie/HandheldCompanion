using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages;

public partial class DpadPage : ILayoutPage
{
    public DpadPage()
    {
        DataContext = new DpadPageViewModel();
        InitializeComponent();
    }
}