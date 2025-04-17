using HandheldCompanion.Converters;
using HandheldCompanion.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    private AverageColorConverter averageColorConverter = new AverageColorConverter();

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
        UpdateArtwork(sender);
    }

    private void ImageContainer_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        UpdateArtwork(sender);
    }

    private void UpdateArtwork(object sender)
    {
        if (sender is Button button && button.DataContext is ProfileViewModel profile)
        {
            LibraryPageViewModel? libraryVM = DataContext as LibraryPageViewModel;
            if (libraryVM != null)
            {
                libraryVM.HighlightColor = (Color)averageColorConverter.Convert(profile.Cover, null, null, null);
                libraryVM.Artwork = profile.Artwork;
            }
        }
    }

    private void ImageContainer_LostFocus(object sender, RoutedEventArgs e)
    {
    }

    private void ImageContainer_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    { }

    public void Page_Closed()
    { }
}