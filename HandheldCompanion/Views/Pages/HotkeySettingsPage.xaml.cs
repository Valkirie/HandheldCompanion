using HandheldCompanion.ViewModels;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public partial class HotkeySettingsPage : Page
{
    public HotkeySettingsPage()
    {
        InitializeComponent();
    }

    public HotkeySettingsPage(string tag) : this()
    {
        this.Tag = tag;
    }

    public void SetHotkey(HotkeyViewModel vm)
    {
        DataContext = vm;
    }
}
