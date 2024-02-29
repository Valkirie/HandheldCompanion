using HandheldCompanion.ViewModels;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public abstract class ILayoutPage : Page
{
    public new bool IsEnabled() => ((ILayoutPageViewModel)DataContext).IsEnabled;
}