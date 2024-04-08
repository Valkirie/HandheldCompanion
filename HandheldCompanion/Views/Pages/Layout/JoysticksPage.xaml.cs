using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages;

public partial class JoysticksPage : ILayoutPage
{
    public JoysticksPage()
    {
        DataContext = new JoysticksPageViewModel();
        InitializeComponent();
    }
}