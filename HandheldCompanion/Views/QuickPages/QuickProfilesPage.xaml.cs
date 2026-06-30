using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickProfilesPage.xaml
/// </summary>
public partial class QuickProfilesPage : Page
{
    private ProfilesPageViewModel viewModel;

    public QuickProfilesPage()
    {
        viewModel = new ProfilesPageViewModel(this);
        DataContext = viewModel;
        InitializeComponent();
    }

    public QuickProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;

        // Subscribe to ViewModel events for UI operations
        viewModel.RequestOpenProfilePage += (s, e) =>
        {
            // Use ViewModel to set selected profile instead of direct control access
            MainWindow.profilesPage.viewModel.SelectedMainProfile = ManagerFactory.profileManager.GetParent(viewModel.SelectedProfile);
            MainWindow.profilesPage.viewModel.SelectedProfile = viewModel.SelectedProfile;

            // Suppress the next transition in MainWindow so the B1 press that opened
            // this navigation is not replayed as a new press once MainWindow gains focus.
            MainWindow.GetCurrent().SuppressNextGamepadInput();
            MainWindow.NavView_Navigate(MainWindow.profilesPage);

            if (MainWindow.GetCurrent().WindowState == WindowState.Minimized)
                MainWindow.GetCurrent().ToggleState();

            if (OverlayQuickTools.GetCurrent().Visibility == Visibility.Visible)
                OverlayQuickTools.GetCurrent().ToggleVisibility();
        };

        viewModel.RequestOpenProfileLayout += (s, e) =>
        {
            if (viewModel.SelectedProfile is null)
                return;

            // Use ViewModel to set selected profile instead of direct control access
            MainWindow.profilesPage.viewModel.SelectedMainProfile = ManagerFactory.profileManager.GetParent(viewModel.SelectedProfile);
            MainWindow.profilesPage.viewModel.SelectedProfile = viewModel.SelectedProfile;
            MainWindow.profilesPage.ControllerSettingsButton_Click(null, null);

            // Suppress the next transition in MainWindow so the B1 press that opened
            // this navigation is not replayed as a new press once MainWindow gains focus.
            MainWindow.GetCurrent().SuppressNextGamepadInput();
            MainWindow.NavView_Navigate(MainWindow.layoutPage);

            if (MainWindow.GetCurrent().WindowState == WindowState.Minimized)
                MainWindow.GetCurrent().ToggleState();

            if (OverlayQuickTools.GetCurrent().Visibility == Visibility.Visible)
                OverlayQuickTools.GetCurrent().ToggleVisibility();
        };

        viewModel.RequestOpenPowerProfile += (s, powerProfile) =>
        {
            OverlayQuickTools.GetCurrent().performancePage.SelectionChanged(powerProfile.Guid);
            OverlayQuickTools.GetCurrent().NavigateToPage("QuickPerformancePage");
        };

        viewModel.RequestCreatePowerProfile += (s, e) =>
        {
            _ = new Dialog(OverlayQuickTools.GetCurrent())
            {
                Title = "Power preset",
                Content = "Power preset was created",
                PrimaryButtonText = Properties.Resources.ProfilesPage_OK
            }.ShowAsync();
        };

        // Wire up the create profile card to show the dialog
        if (CreatePowerProfileCard is not null)
        {
            CreatePowerProfileCard.Click += async (s, e) =>
            {
                // Initialize the form first
                viewModel.ShowCreateProfileFlyoutCommand.Execute(null);

                // Show the ContentDialog from resources
                var dialog = Resources["CreatePowerProfileDialog"] as ContentDialog;
                if (dialog is not null)
                {
                    // Ensure the dialog has the correct data context
                    dialog.DataContext = viewModel;
                    dialog.Owner = Window.GetWindow(this);
                    ContentDialogResult result = ContentDialogResult.None;

                    try { result = await dialog.ShowAsync(); } catch { }

                    if (result == ContentDialogResult.Primary)
                    {
                        // Execute the create command
                        viewModel.CreatePowerProfileCommand.Execute(null);
                    }
                }
            };
        }
    }

    private void Page_Loaded(object s, RoutedEventArgs e)
    {
        // do something
    }

    private void Page_Unloaded(object s, RoutedEventArgs e)
    {
        // do something
    }

    public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
    {
        viewModel.PowerProfile_Selected(powerProfile, AC);
    }
}

