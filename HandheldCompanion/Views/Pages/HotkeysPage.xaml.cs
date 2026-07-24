using HandheldCompanion.ViewModels;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for HotkeysPage.xaml
/// </summary>
public partial class HotkeysPage : Page
{
    public static HotkeySettingsPage hotkeySettingsPage = null!;

    public HotkeysPage()
    {
        hotkeySettingsPage = new HotkeySettingsPage("hotkeySettings");
        DataContext = new HotkeyPageViewModel();
        InitializeComponent();
    }

    public HotkeysPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public void Dispose()
    {
        ((HotkeyPageViewModel)DataContext).Dispose();
    }
}