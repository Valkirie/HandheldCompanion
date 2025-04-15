using HandheldCompanion.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    public LibraryPage()
    {
        Tag = "about";
        DataContext = new LibraryPageViewModel();
        InitializeComponent();
    }

    public LibraryPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    { }

    public void Page_Closed()
    { }
}