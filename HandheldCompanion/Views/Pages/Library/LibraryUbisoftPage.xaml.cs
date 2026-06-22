using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Library;

public partial class LibraryUbisoftPage : Page, ILibraryRoutedPage
{
    public string NavigationKey { get; } = LibraryNavigationKeys.Platform(GamePlatform.UbisoftConnect);

    public LibraryUbisoftPage(LibraryPageViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
