using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
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
    }

    private void Page_Loaded(object s, RoutedEventArgs e)
    {
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
            new Dialog(OverlayQuickTools.GetCurrent())
            {
                Title = "Power preset",
                Content = "Power preset was created",
                PrimaryButtonText = Properties.Resources.ProfilesPage_OK
            }.ShowAsync();
        };
    }

    private void Page_Unloaded(object s, RoutedEventArgs e)
    {
        // Unsubscribe from ViewModel events
        viewModel.RequestOpenProfilePage -= (s, e) => { };
        viewModel.RequestOpenProfileLayout -= (s, e) => { };
        viewModel.RequestOpenPowerProfile -= (s, powerProfile) => { };
        viewModel.RequestCreatePowerProfile -= (s, e) => { };
        viewModel.Close();
    }

    public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
    {
        viewModel.PowerProfile_Selected(powerProfile, AC);
    }
}

