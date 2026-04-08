using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickProfilesPage.xaml
/// </summary>
public partial class QuickProfilesPage : Page
{
    private ProfilesPageViewModel viewModel;

    public QuickProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickProfilesPage()
    {
        viewModel = new ProfilesPageViewModel(this);
        DataContext = viewModel;
        InitializeComponent();

        // Subscribe to ViewModel events for UI operations
        viewModel.RequestOpenProfilePage += (s, e) =>
        {
            // Use ViewModel to set selected profile instead of direct control access
            MainWindow.profilesPage.viewModel.SelectedMainProfile = ManagerFactory.profileManager.GetParent(viewModel.SelectedProfile);
            MainWindow.profilesPage.viewModel.SelectedProfile = viewModel.SelectedProfile;
            MainWindow.NavView_Navigate(MainWindow.profilesPage);

            if (MainWindow.GetCurrent().WindowState == WindowState.Minimized)
                MainWindow.GetCurrent().ToggleState();

            if (ManagerFactory.settingsManager.GetBoolean("QuickToolsAutoHide") && OverlayQuickTools.GetCurrent().Visibility == Visibility.Visible)
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
            MainWindow.NavView_Navigate(MainWindow.layoutPage);

            if (MainWindow.GetCurrent().WindowState == WindowState.Minimized)
                MainWindow.GetCurrent().ToggleState();

            if (ManagerFactory.settingsManager.GetBoolean("QuickToolsAutoHide") && OverlayQuickTools.GetCurrent().Visibility == Visibility.Visible)
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

        switch (ManagerFactory.platformManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPlatforms();
                break;
        }

        foreach (var mode in Enum.GetValues<MotionOutput>())
        {
            var comboBoxItem = new ComboBoxItem()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };

            var simpleStackPanel = new SimpleStackPanel
            {
                Spacing = 6,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var icon = new FontIcon() { Glyph = mode.ToGlyph() };
            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            simpleStackPanel.Children.Add(text);

            comboBoxItem.Content = simpleStackPanel;
            MotionOutputComboBox.Items.Add(comboBoxItem);
        }

        foreach (var mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
        {
            var comboBoxItem = new ComboBoxItem()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };

            var simpleStackPanel = new SimpleStackPanel
            {
                Spacing = 6,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var icon = new FontIcon() { Glyph = mode.ToGlyph() };
            if (!string.IsNullOrEmpty(icon.Glyph))
                simpleStackPanel.Children.Add(icon);

            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            simpleStackPanel.Children.Add(text);

            comboBoxItem.Content = simpleStackPanel;
            MotionInputComboBox.Items.Add(comboBoxItem);
        }
    }

    public void Close()
    {
        viewModel.Close();
    }

    public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
    {
        viewModel.PowerProfile_Selected(powerProfile, AC);
    }
}
