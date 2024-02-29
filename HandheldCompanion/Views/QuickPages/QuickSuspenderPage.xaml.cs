using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickSuspenderPage : Page
{
    public QuickSuspenderPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickSuspenderPage()
    {
        DataContext = new QuickSuspenderPageViewModel();
        InitializeComponent();
    }
}