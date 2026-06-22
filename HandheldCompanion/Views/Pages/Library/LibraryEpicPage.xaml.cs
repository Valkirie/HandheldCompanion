using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryEpicPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; } = LibraryNavigationKeys.Platform(GamePlatform.Epic);

    public LibraryEpicPage(LibraryPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
