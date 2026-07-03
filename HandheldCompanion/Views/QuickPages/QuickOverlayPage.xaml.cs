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

        ViewModel = new OverlayPageViewModel();
        DataContext = ViewModel;
        InitializeComponent();

        this.Loaded += OverlayPage_Loaded;
        this.Unloaded += OverlayPage_Unloaded;
    }

    private void OverlayPage_Loaded(object sender, RoutedEventArgs e) => ViewModel.OnPageLoaded();

    private void OverlayPage_Unloaded(object sender, RoutedEventArgs e) => ViewModel.OnPageUnloaded();
}