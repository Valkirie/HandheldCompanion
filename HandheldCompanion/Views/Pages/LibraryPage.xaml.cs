using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Pages.Library;
using iNKORE.UI.WPF.Modern.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    private LibraryPageViewModel? ViewModel => DataContext as LibraryPageViewModel;

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
    {
        if (ViewModel is { } vm)
        {
            vm.BackAvailabilityChanged += LibraryPageViewModel_BackAvailabilityChanged;

            if (vm is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += LibraryPageViewModel_PropertyChanged;
        }

        navView.SelectedItem = ViewModel?.NavigationViewSelectedItem;
        NavigateToSelectedPage();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.BackAvailabilityChanged -= LibraryPageViewModel_BackAvailabilityChanged;

            if (vm is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= LibraryPageViewModel_PropertyChanged;
        }
    }

    public void Page_Closed()
    { }

    private void LibraryPageViewModel_BackAvailabilityChanged(bool canGoBack)
    {
    }

    private void LibraryPageViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryPageViewModel.SelectedNavigationItem) ||
            e.PropertyName == nameof(LibraryPageViewModel.NavigationViewSelectedItem))
        {
            NavigateToSelectedPage();
        }
    }

    private void NavigateToSelectedPage()
    {
        if (ViewModel?.SelectedNavigationItem is null)
            return;

        string targetKey = ViewModel.SelectedNavigationItem.Key;
        if (ContentFrame.Content is ILibraryRoutedPage currentPage && string.Equals(currentPage.NavigationKey, targetKey, System.StringComparison.Ordinal))
            return;

        Page nextPage = CreatePageForSelection(ViewModel.SelectedNavigationItem);
        ContentFrame.Navigate(nextPage);
    }

    public bool TryGoBack()
    {
        return ViewModel?.TryGoBack() ?? false;
    }

    public void NavView_Navigate(string navItemTag)
    {
        if (ViewModel?.SelectNavigationItemByKey(navItemTag) == true)
            NavigateToSelectedPage();
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem navItem || navItem.Tag is not string key)
            return;

        ViewModel?.SelectNavigationItemByKey(key);
    }

    private void navView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            // The NavigationView may auto-select the first (disabled L2) item on load.
            // Restore the correct selection from the ViewModel.
            navView.SelectedItem = vm.NavigationViewSelectedItem;
        }

        NavigateToSelectedPage();
    }

    private Page CreatePageForSelection(LibraryNavigationItemViewModel selection)
    {
        return selection.Key switch
        {
            LibraryNavigationKeys.AllGames => new LibraryAllGamesPage(ViewModel!),
            LibraryNavigationKeys.Favorites => new LibraryFavoritesPage(ViewModel!),
            LibraryNavigationKeys.Collections => new LibraryCollectionsOverviewPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Collection && selection.CollectionId.HasValue
                => new LibraryCollectionPage(ViewModel!, selection.CollectionId.Value),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.BattleNet
                => new LibraryBattleNetPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.EADesktop
                => new LibraryEADesktopPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.Epic
                => new LibraryEpicPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.GOG
                => new LibraryGOGPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.MicrosoftStore
                => new LibraryMicrosoftStorePage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.Origin
                => new LibraryOriginPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.RiotGames
                => new LibraryRiotPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.Rockstar
                => new LibraryRockstarPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.Steam
                => new LibrarySteamPage(ViewModel!),
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform == GamePlatform.UbisoftConnect
                => new LibraryUbisoftPage(ViewModel!),
            _ => new LibraryAllGamesPage(ViewModel!)
        };
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is not ILibraryRoutedPage routedPage || ViewModel is null)
            return;

        ViewModel.SelectNavigationItemByKey(routedPage.NavigationKey);
    }
}
