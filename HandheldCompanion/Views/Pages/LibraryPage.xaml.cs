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

    private void ImageContainer_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ProfileViewModel profile)
        {
            // Assume you have defined a property in your LibraryPageViewModel
            // called BackgroundImage of type BitmapImage.
            // Here we update that property with the Artwork image:
            var libraryVM = DataContext as LibraryPageViewModel;
            if (libraryVM != null)
            {
                libraryVM.BackgroundImage = profile.Artwork;
            }
        }
    }

    private void ImageContainer_LostFocus(object sender, RoutedEventArgs e)
    {
        // Optionally: reset or change the background when focus leaves the button.
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    { }

    public void Page_Closed()
    { }
}