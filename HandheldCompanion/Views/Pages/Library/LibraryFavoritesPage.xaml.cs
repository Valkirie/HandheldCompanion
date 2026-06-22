using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryFavoritesPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; } = LibraryNavigationKeys.Favorites;

    public LibraryFavoritesPage(LibraryPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
