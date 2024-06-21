using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickApplicationsPage : Page
{
    public QuickApplicationsPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickApplicationsPage()
    {
        DataContext = new QuickApplicationsPageViewModel();
        InitializeComponent();
    }
}