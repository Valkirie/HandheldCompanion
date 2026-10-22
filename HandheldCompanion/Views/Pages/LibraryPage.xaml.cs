using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Pages.Library;
using iNKORE.UI.WPF.Modern.Controls;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;

using Control = System.Windows.Controls.Control;
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
            vm.FocusedProfileReplaced += LibraryPageViewModel_FocusedProfileReplaced;

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
            vm.FocusedProfileReplaced -= LibraryPageViewModel_FocusedProfileReplaced;
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

    private void LibraryPageViewModel_FocusedProfileReplaced(ProfileViewModel? profile)
    {
        // The deleted card took keyboard focus with it: focus the replacement's card once layout and any dialog have settled,
        // or the first toolbar control when the library is now empty
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsLoaded)
                return;

            if (profile is null)
            {
                SortComboBox.Focus();
                return;
            }

            Control? card = WPFUtils.FindVisualChildren<Control>(ContentFrame)
                .Where(control => control.Focusable && control.IsVisible && control.IsEnabled)
                .FirstOrDefault(control => ReferenceEquals(control.Tag, profile)
                    || (control.Tag is null && ReferenceEquals(control.DataContext, profile)));

            card?.Focus();
        }, DispatcherPriority.ApplicationIdle);
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
