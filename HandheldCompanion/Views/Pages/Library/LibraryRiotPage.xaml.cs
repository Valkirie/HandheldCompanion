using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryRiotPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; } = LibraryNavigationKeys.Platform(GamePlatform.RiotGames);

    public LibraryRiotPage(LibraryPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
