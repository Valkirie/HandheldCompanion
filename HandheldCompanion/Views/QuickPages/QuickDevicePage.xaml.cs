using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System.Threading.Tasks;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickDevicePage.xaml
/// </summary>
public partial class QuickDevicePage : Page
{
    private QuickDevicePageViewModel? ViewModel;

    public QuickDevicePage()
    {
        InitializeComponent();

        ViewModel = new QuickDevicePageViewModel(this);
        DataContext = ViewModel;
    }

    public QuickDevicePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void Page_Loaded(object s, RoutedEventArgs e)
    {
        // Subscribe to ViewModel events
        ViewModel?.RequestAYANEOFlipScreenConfirmation += ViewModel_RequestAYANEOFlipScreenConfirmation;
    }

    private void Page_Unloaded(object s, RoutedEventArgs e)
    {
        // Unsubscribe from all events
        ViewModel?.RequestAYANEOFlipScreenConfirmation -= ViewModel_RequestAYANEOFlipScreenConfirmation;
    }

    private async void ViewModel_RequestAYANEOFlipScreenConfirmation(object? sender, TaskCompletionSource<bool> tcs)
    {
        // todo: translate me
        Task<ContentDialogResult> dialogTask = new Dialog(OverlayQuickTools.GetCurrent())
        {
            Title = "Warning",
            Content = "To reactivate the lower screen, press the dual screen button on your device.",
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_OK
        }.ShowAsync();

        await dialogTask; // sync call

        // Set the result based on the dialog outcome
        bool confirmed = dialogTask.Result == ContentDialogResult.Primary;
        tcs.SetResult(confirmed);
    }
}
