using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.ViewModels.Windows;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Pages.Welcome;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Navigation;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Windows;

public partial class WelcomeWindow : GamepadWindow
{
    private const string WelcomeKey = "Welcome";
    private const string DeviceKey = "Device";
    private const string OemKey = "OEM";
    private const string SettingsKey = "Settings";
    private const string DoneKey = "Done";

    private readonly Dictionary<string, Page> pages;
    private readonly List<string> pageOrder = [WelcomeKey, DeviceKey, OemKey, SettingsKey, DoneKey];
    private readonly WelcomeViewModel viewModel;
    private int currentPageIndex;

    public override string HomePageKey => WelcomeKey;
    public bool Completed { get; private set; }
    public bool RestartRequired => viewModel.RestartRequired;

    public WelcomeWindow(IDevice device)
    {
        viewModel = new WelcomeViewModel(device);
        DataContext = viewModel;

        ElementTheme currentTheme = (ElementTheme)ManagerFactory.settingsManager.GetInt("MainWindowTheme");
        ThemeManager.SetRequestedTheme(this, currentTheme);

        InitializeComponent();
        Tag = "WelcomeWindow";

        pages = new Dictionary<string, Page>
        {
            [WelcomeKey] = new WelcomeStartPage { DataContext = viewModel },
            [DeviceKey] = new WelcomeDevicePage { DataContext = viewModel },
            [OemKey] = new WelcomeOemPage { DataContext = viewModel },
            [SettingsKey] = new WelcomeSettingsPage { DataContext = viewModel },
            [DoneKey] = new WelcomeFinishPage { DataContext = viewModel },
        };

        ContentFrame.Navigated += ContentFrame_Navigated;
        gamepadFocusManager = new(this, (Frame)ContentFrame, navView);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Clear WS_MAXIMIZE so Windows does not show this window maximized when first
        // displayed. This has no ShowWindow side-effect unlike SetWindowPlacement.
        WinAPI.ClearMaximizeStyle(new WindowInteropHelper(this).Handle);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        gamepadFocusManager.Loaded();
        NavigateToPage(WelcomeKey);
    }

    public override void NavigateToPage(string navItemTag)
    {
        if (!pages.TryGetValue(navItemTag, out Page? page))
            return;

        currentPageIndex = pageOrder.IndexOf(navItemTag);
        viewModel.IsFirstPage = currentPageIndex == 0;
        viewModel.IsLastPage = currentPageIndex == pageOrder.Count - 1;
        ContentFrame.Navigate(page);
        UpdateNavigationSelection(navItemTag);
        UpdateButtons();
    }

    protected override void ApplyPendingNavigation(string navItemTag)
    {
        NavigateToPage(navItemTag);
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is not string tag)
            return;

        NavigateToPage(tag);
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        UpdateButtons();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentPageIndex <= 0)
            return;

        NavigateToPage(pageOrder[currentPageIndex - 1]);
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentPageIndex < pageOrder.Count - 1)
        {
            NavigateToPage(pageOrder[currentPageIndex + 1]);
            return;
        }

        CompleteOnboarding(true);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        CompleteOnboarding(false);
    }

    private void CompleteOnboarding(bool completed)
    {
        Completed = completed;
        viewModel.Commit(completed);
        DialogResult = completed;
        Close();
    }

    private void UpdateNavigationSelection(string tag)
    {
        foreach (object item in navView.MenuItems)
        {
            if (item is NavigationViewItem navigationViewItem && navigationViewItem.Tag as string == tag)
            {
                navView.SelectedItem = navigationViewItem;
                break;
            }
        }
    }

    private void UpdateButtons()
    {
        if (BackButton is null)
            return;

        BackButton.IsEnabled = currentPageIndex > 0;
    }
}
