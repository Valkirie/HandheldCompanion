using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views.Pages.Profiles;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Linq;
using System.Windows;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    private static ProfilesPage instance;

    // Legacy property for backward compatibility - delegates to ViewModel
    public static Profile selectedProfile => instance?.viewModel?.SelectedProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");

    public ProfilesPageViewModel viewModel;

    public ProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public ProfilesPage()
    {
        instance = this;
        viewModel = new ProfilesPageViewModel(this);
        DataContext = viewModel;
        InitializeComponent();

        // Subscribe to control changes for Profile properties that are bound directly
        Loaded += ProfilesPage_Loaded;

        // Subscribe to ViewModel events for UI operations
        viewModel.RequestCreateProfile += (s, e) => b_CreateProfile_Click(null, null);
        viewModel.RequestDeleteProfile += (s, profile) => b_DeleteProfile_Click(null, null);
        viewModel.RequestRenameProfile += (s, profile) => b_ProfileRename_Click(null, null);
        viewModel.RequestCreateSubProfile += (s, e) => b_SubProfileCreate_Click(null, null);
        viewModel.RequestDeleteSubProfile += (s, profile) => b_SubProfileDelete_Click(null, null);
        viewModel.RequestRenameSubProfile += (s, profile) => b_SubProfileRename_Click(null, null);
        viewModel.RequestOpenControllerLayout += (s, template) =>
        {
            MainWindow.layoutPage.UpdateLayoutTemplate(template);
            MainWindow.NavView_Navigate(MainWindow.layoutPage);
        };
        viewModel.RequestOpenPowerProfile += (s, powerProfile) =>
        {
            MainWindow.performancePage.SelectionChanged(powerProfile);
            MainWindow.GetCurrent().NavigateToPage("PerformancePage");
        };
        viewModel.RequestOpenAdditionalSettings += (s, e) => b_AdditionalSettings_Click(null, null);
    }

    private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Subscribe to UI control changes for properties bound directly to SelectedProfile
        EnableProfileToggle.Toggled += ProfileProperty_Changed;
        WhitelistToggle.Toggled += ProfileProperty_Changed;
        EmulatedControllerComboBox.SelectionChanged += ProfileProperty_Changed;
        WrapperComboBox.SelectionChanged += ProfileProperty_Changed;
        GPUScalingToggle.Toggled += ProfileProperty_Changed;
        GPUScalingComboBox.SelectionChanged += ProfileProperty_Changed;
        AFMFToggle.Toggled += ProfileProperty_Changed;
        RSRToggle.Toggled += ProfileProperty_Changed;
        IntegerScalingToggle.Toggled += ProfileProperty_Changed;
        RISToggle.Toggled += ProfileProperty_Changed;
        GyroSteeringComboBox.SelectionChanged += ProfileProperty_Changed;
        InvertHorizontalCheckBox.Checked += ProfileProperty_Changed;
        InvertHorizontalCheckBox.Unchecked += ProfileProperty_Changed;
        InvertVerticalCheckBox.Checked += ProfileProperty_Changed;
        InvertVerticalCheckBox.Unchecked += ProfileProperty_Changed;
        SuspendOnOverlayCheckBox.Checked += ProfileProperty_Changed;
        SuspendOnOverlayCheckBox.Unchecked += ProfileProperty_Changed;
        SuspendOnSleepCheckBox.Checked += ProfileProperty_Changed;
        SuspendOnSleepCheckBox.Unchecked += ProfileProperty_Changed;
        UseFullscreenOptimizations.Toggled += ProfileProperty_Changed;
        UseHighDPIAwareness.Toggled += ProfileProperty_Changed;
        ShowInLibraryToggle.Toggled += ProfileProperty_Changed;
    }

    private void ProfileProperty_Changed(object sender, RoutedEventArgs e)
    {
        // Only trigger update if we're not loading the profile (prevent updates during UI refresh)
        if (!viewModel.IsLoadingProfile)
            viewModel.UpdateProfile();
    }

    public void Page_Closed()
    {
        viewModel.Close();
    }

    // Navigation and dialog events that cannot be bound
    public async void b_CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        viewModel.UpdateProfile();

        try
        {
            string path = string.Empty;
            string arguments = string.Empty;
            string name = string.Empty;

            FileUtils.CommonFileDialog(out path, out arguments, out name);

            Profile profile = new Profile(path);
            profile.Arguments = arguments;
            if (!string.IsNullOrEmpty(arguments))
                profile.Name = name;

            bool exists = false;
            Profile parentProfile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (parentProfile is not null && !parentProfile.Default)
            {
                var dialogTask = new Dialog(MainWindow.GetCurrent())
                {
                    Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, parentProfile.Name),
                    Content = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, parentProfile.Name),
                    CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                    PrimaryButtonText = Properties.Resources.ProfilesPage_Yes,
                    SecondaryButtonText = Properties.Resources.ProfilesPage_AreYouSureOverwriteSecondary,
                }.ShowAsync();

                await dialogTask;

                switch (dialogTask.Result)
                {
                    case ContentDialogResult.Primary:
                        exists = false;
                        break;
                    case ContentDialogResult.Secondary:
                        {
                            Profile mainProfile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
                            profile.IsSubProfile = true;
                            profile.ParentGuid = mainProfile.Guid;
                            exists = false;
                        }
                        break;
                    default:
                        exists = true;
                        break;
                }
            }

            if (!exists)
                ManagerFactory.profileManager.UpdateOrCreateProfile(profile, UpdateSource.Creation);
        }
        catch (Exception ex)
        {
            LogManager.LogError(ex.Message);
        }
    }

    public void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProfile is null)
            return;

        if (!viewModel.SelectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        switch (((GyroActions)currentAction).MotionInput)
        {
            default:
                page0.SetProfile();
                MainWindow.NavView_Navigate(page0);
                break;
            case MotionInput.JoystickSteering:
                page1.SetProfile();
                MainWindow.NavView_Navigate(page1);
                break;
        }
    }

    public void PowerProfile_Selected(PowerProfile powerProfile, bool AC)
    {
        viewModel.PowerProfile_Selected(powerProfile, AC);
    }

    public async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProfile is null)
            return;

        var dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureDelete1, viewModel.SelectedMainProfile.Name),
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask;

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ManagerFactory.profileManager.DeleteProfile(viewModel.SelectedMainProfile);
                // Select first profile via ViewModel instead of direct control access
                if (viewModel.MainProfiles.Any())
                    viewModel.SelectedMainProfile = viewModel.MainProfiles[0];
                break;
        }
    }

    public void ControllerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedProfile is null)
            return;

        var selectedTemplate = new LayoutTemplate(viewModel.SelectedProfile.Layout)
        {
            Name = viewModel.SelectedProfile.LayoutTitle,
            Description = Properties.Resources.ProfilesPage_Layout_Desc,
            Author = Environment.UserName,
            Executable = viewModel.SelectedProfile.Executable,
            Product = viewModel.SelectedProfile.Name,
        };

        MainWindow.layoutPage.UpdateLayoutTemplate(selectedTemplate);
        MainWindow.NavView_Navigate(MainWindow.layoutPage);
    }

    public void b_SubProfileCreate_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedMainProfile is null)
            return;

        Profile newSubProfile = (Profile)viewModel.SelectedMainProfile.Clone();
        newSubProfile.Name = Properties.Resources.ProfilesPage_NewSubProfile;
        newSubProfile.Guid = Guid.NewGuid();
        newSubProfile.IsSubProfile = true;
        newSubProfile.ParentGuid = viewModel.SelectedMainProfile.Guid;

        ManagerFactory.profileManager.UpdateOrCreateProfile(newSubProfile);
    }

    public async void b_SubProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        // Use ViewModel instead of direct control access
        if (viewModel.SelectedProfile == null || !viewModel.SelectedProfile.IsSubProfile)
            return;

        Profile subProfile = viewModel.SelectedProfile;

        var dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureDelete1, subProfile.Name),
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask;

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ManagerFactory.profileManager.DeleteProfile(subProfile);
                break;
        }
    }

    public void b_SubProfileRename_Click(object sender, RoutedEventArgs e)
    {
        SubProfileNameTextBox.Text = viewModel.SelectedProfile?.Name ?? "";
        SubProfileRenameDialog.ShowAsync();
    }

    private void SubProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (viewModel.SelectedProfile is null)
            return;

        viewModel.SelectedProfile.Name = SubProfileNameTextBox.Text;
        viewModel.SubmitProfile(UpdateSource.ProfilesPageUpdateOnly);
    }

    private void SubProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SubProfileNameTextBox.Text = viewModel.SelectedProfile?.Name ?? "";
    }

    private void ProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ProfileNameTextBox.Text = viewModel.SelectedMainProfile?.Name ?? "";
    }

    private void ProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (viewModel.SelectedMainProfile is null)
            return;

        viewModel.SelectedMainProfile.Name = ProfileNameTextBox.Text;
        viewModel.SubmitProfile(UpdateSource.ProfilesPageUpdateOnly);
    }

    public void b_ProfileRename_Click(object sender, RoutedEventArgs e)
    {
        ProfileNameTextBox.Text = viewModel.SelectedMainProfile?.Name ?? "";
        ProfileRenameDialog.ShowAsync();
    }

    public static void SubmitProfile()
    {
        if (selectedProfile == null)
            return;

        ManagerFactory.profileManager.UpdateOrCreateProfile(selectedProfile, UpdateSource.ProfilesPage);
    }
}
