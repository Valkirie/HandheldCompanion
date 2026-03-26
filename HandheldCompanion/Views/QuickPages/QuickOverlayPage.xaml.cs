using HandheldCompanion.ViewModels;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickOverlayPage : Page
{
    public QuickOverlayPage()
    {
        Tag = "quickoverlay";
        DataContext = new OverlayPageViewModel();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        ((OverlayPageViewModel)DataContext).OnNavigatedTo();

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        ((OverlayPageViewModel)DataContext).OnNavigatedFrom();
}