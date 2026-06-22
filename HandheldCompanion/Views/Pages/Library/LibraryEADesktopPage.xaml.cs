using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryEADesktopPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; } = LibraryNavigationKeys.Platform(GamePlatform.EADesktop);

    public LibraryEADesktopPage(LibraryPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
