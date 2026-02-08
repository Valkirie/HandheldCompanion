using HandheldCompanion.ViewModels;

namespace HandheldCompanion.Views.Pages;

public partial class ActionSettingsPage : System.Windows.Controls.Page
{
    private MappingViewModel? _mapping;

    public ActionSettingsPage()
    {
        InitializeComponent();
    }

    public void SetMapping(MappingViewModel mapping)
    {
        _mapping = mapping;
        DataContext = mapping;
    }
}

