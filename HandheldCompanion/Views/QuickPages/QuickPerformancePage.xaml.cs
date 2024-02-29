using HandheldCompanion.Managers;
using HandheldCompanion.ViewModels;
using System;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickPerformancePage : Page
{
    public QuickPerformancePage()
    {
        Tag = "quickperformance";
        DataContext = new PerformancePageViewModel(isQuickTools: true);
        InitializeComponent();
    }

    public void SelectionChanged(Guid guid)
    {
        ((PerformancePageViewModel)DataContext).SelectedPreset = PowerProfileManager.GetProfile(guid);
    }
}