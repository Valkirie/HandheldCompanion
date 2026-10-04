using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Pages.Library;
using iNKORE.UI.WPF.Modern.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Navigation;

using Page = System.Windows.Controls.Page;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace HandheldCompanion.Views.Pages;

public partial class LibraryPage : Page
{
    private LibraryPageViewModel? ViewModel => DataContext as LibraryPageViewModel;
    private ScrollViewer? hostScrollViewer;

    public LibraryPage()
    {
        Tag = "about";
        DataContext = new LibraryPageViewModel();
        InitializeComponent();

        Loaded += LibraryPage_Loaded;
        Unloaded += LibraryPage_Unloaded;

        if (ViewModel is { } vm)
        {
            vm.BackAvailabilityChanged += LibraryPageViewModel_BackAvailabilityChanged;

            if (vm is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += LibraryPageViewModel_PropertyChanged;
        }

        navView.SelectedItem = ViewModel?.NavigationViewSelectedItem;
        NavigateToSelectedPage();
    }

    public LibraryPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public void Dispose()
    {
        DetachHostScrollViewer();

        if (ViewModel is { } vm)
        {
            vm.BackAvailabilityChanged -= LibraryPageViewModel_BackAvailabilityChanged;
            vm.ClearFocusedProfile();

            if (vm is INotifyPropertyChanged inpc)
                inpc.PropertyChanged -= LibraryPageViewModel_PropertyChanged;
        }
    }

    private void LibraryPage_Loaded(object sender, RoutedEventArgs e)
    {
        DetachHostScrollViewer();
        hostScrollViewer = WPFUtils.FindParent<ScrollViewer>(this);

        if (hostScrollViewer is null)
            return;

        hostScrollViewer.SizeChanged += HostScrollViewer_SizeChanged;
        UpdateViewportHeight();
    }

    private void LibraryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachHostScrollViewer();
    }

    private void HostScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateViewportHeight();
    }

    private void UpdateViewportHeight()
    {
        if (ViewModel is { } vm && hostScrollViewer is not null)
            vm.ViewportHeight = hostScrollViewer.ActualHeight;
    }

    private void DetachHostScrollViewer()
    {
        hostScrollViewer?.SizeChanged -= HostScrollViewer_SizeChanged;

        hostScrollViewer = null;
    }

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

    public void UpdateFocusedProfile(ProfileViewModel profile)
    {
        ViewModel?.UpdateFocusedProfile(profile);
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
            _ when selection.Kind == LibraryNavigationItemKind.Platform && selection.Platform is
                GamePlatform.BattleNet or
                GamePlatform.EADesktop or
                GamePlatform.Epic or
                GamePlatform.GOG or
                GamePlatform.MicrosoftStore or
                GamePlatform.Origin or
                GamePlatform.RiotGames or
                GamePlatform.Rockstar or
                GamePlatform.Steam or
                GamePlatform.UbisoftConnect
                => new LibraryPlatformPage(ViewModel!, selection.Platform),
            _ when selection.Kind == LibraryNavigationItemKind.Platform
                => new LibraryEmulatorPage(ViewModel!, selection.Platform),
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
