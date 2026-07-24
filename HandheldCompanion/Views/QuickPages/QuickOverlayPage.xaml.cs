using HandheldCompanion.ViewModels;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

public partial class QuickOverlayPage : Page
{
    private readonly OverlayPageViewModel ViewModel;

    public QuickOverlayPage()
    {
        Tag = "quickoverlay";

        ViewModel = new OverlayPageViewModel(true);
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OverlayPage_Loaded;
        Unloaded += OverlayPage_Unloaded;
    }

    private void OverlayPage_Loaded(object sender, RoutedEventArgs e) => ViewModel.OnPageLoaded();

    private void OverlayPage_Unloaded(object sender, RoutedEventArgs e) => ViewModel.OnPageUnloaded();

    public void Dispose() => ViewModel.OnPageUnloaded();
}