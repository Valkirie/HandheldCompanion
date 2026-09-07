using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryPlatformPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; }

    public LibraryPlatformPage(LibraryPageViewModel viewModel, GamePlatform platform)
    {
        DataContext = viewModel;
        NavigationKey = LibraryNavigationKeys.Platform(platform);
        InitializeComponent();
    }
}
