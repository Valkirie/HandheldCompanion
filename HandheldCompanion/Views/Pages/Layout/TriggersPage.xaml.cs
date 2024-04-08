using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages;

public partial class TriggersPage : ILayoutPage
{
    public TriggersPage()
    {
        DataContext = new TriggersPageViewModel();
        InitializeComponent();
    }
}