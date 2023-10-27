using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Pages.Profiles;
using Inkore.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using static HandheldCompanion.Utils.XInputPlusUtils;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    // when set on start cannot be null anymore
    public static Profile selectedProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");

    private LockObject updateLock = new();

    private const int UpdateInterval = 500;
    private static Timer UpdateTimer;

    public ProfilesPage()
    {
        InitializeComponent();

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();
    }

    public ProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;

        ProfileManager.Deleted += ProfileDeleted;
        ProfileManager.Updated += ProfileUpdated;
        ProfileManager.Applied += ProfileApplied;

        PowerProfileManager.Updated += PowerProfileManager_Updated;
        PowerProfileManager.Deleted += PowerProfileManager_Deleted;

        ProfileManager.Initialized += ProfileManagerLoaded;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SystemManager.RSRStateChanged += SystemManager_RSRStateChanged;

        // auto-sort
        cB_Profiles.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Descending));
    }

    private void SystemManager_RSRStateChanged(int RSRState, int RSRSharpness)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (RSRState)
            {
                case -1:
                    RSRToggle.IsEnabled = false;
                    break;
                case 0:
                    RSRToggle.IsEnabled = true;
                    RSRToggle.IsOn = false;
                    RSRSlider.Value = RSRSharpness;
                    break;
                case 1:
                    RSRToggle.IsEnabled = true;
                    RSRToggle.IsOn = true;
                    RSRSlider.Value = RSRSharpness;
                    break;
            }
        });
    }

    public void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
            }
        });
    }

    private void SystemManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var screenFrequency = SystemManager.GetDesktopScreen().GetFrequency();

            FramerateQuarter.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Quarter));
            FramerateThird.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Third));
            FramerateHalf.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Half));
            FramerateFull.Content = Convert.ToString(screenFrequency.GetValue(Frequency.Full));
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }

    private async void b_CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        // if an update is pending, execute it and stop timer
        if (UpdateTimer.Enabled)
            UpdateTimer.Stop();

        var openFileDialog = new OpenFileDialog()
        {
            Filter = "Executable|*.exe|UWP manifest|AppxManifest.xml",
        };

        if (openFileDialog.ShowDialog() == true)
            try
            {
                string path = openFileDialog.FileName;
                string folder = Path.GetDirectoryName(path);

                string file = openFileDialog.SafeFileName;
                string ext = Path.GetExtension(file);

                switch (ext)
                {
                    default:
                    case ".exe":
                        break;
                    case ".xml":
                        try
                        {
                            XmlDocument doc = new XmlDocument();
                            string UWPpath = string.Empty;
                            string UWPfile = string.Empty;

                            // check if MicrosoftGame.config exists
                            string configPath = Path.Combine(folder, "MicrosoftGame.config");
                            if (File.Exists(configPath))
                            {
                                doc.Load(configPath);

                                XmlNodeList ExecutableList = doc.GetElementsByTagName("ExecutableList");
                                foreach (XmlNode node in ExecutableList)
                                    foreach (XmlNode child in node.ChildNodes)
                                        if (child.Name.Equals("Executable"))
                                            if (child.Attributes is not null)
                                                foreach (XmlAttribute attribute in child.Attributes)
                                                    switch (attribute.Name)
                                                    {
                                                        case "Name":
                                                            UWPpath = Path.Combine(folder, attribute.InnerText);
                                                            UWPfile = Path.GetFileName(path);
                                                            break;
                                                    }
                            }

                            // either there was no config file, either we couldn't find an executable within it
                            if (!File.Exists(UWPpath))
                            {
                                doc.Load(path);

                                XmlNodeList Applications = doc.GetElementsByTagName("Applications");
                                foreach (XmlNode node in Applications)
                                    foreach (XmlNode child in node.ChildNodes)
                                        if (child.Name.Equals("Application"))
                                            if (child.Attributes is not null)
                                                foreach (XmlAttribute attribute in child.Attributes)
                                                    switch (attribute.Name)
                                                    {
                                                        case "Executable":
                                                            UWPpath = Path.Combine(folder, attribute.InnerText);
                                                            UWPfile = Path.GetFileName(path);
                                                            break;
                                                    }
                            }

                            // we're good to go
                            if (File.Exists(UWPpath))
                            {
                                path = UWPpath;
                                file = UWPfile;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogError(ex.Message, true);
                        }

                        break;
                }

                Profile profile = new Profile(path)
                {
                    Layout = LayoutTemplate.DefaultLayout.Layout.Clone() as Layout,
                    LayoutTitle = LayoutTemplate.DefaultLayout.Name,
                };

                var exists = false;

                // check on path rather than profile
                if (ProfileManager.Contains(path))
                {
                    var result = Dialog.ShowAsync(
                        string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, profile.Name),
                        string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, profile.Name),
                        ContentDialogButton.Primary,
                        $"{Properties.Resources.ProfilesPage_Cancel}",
                        $"{Properties.Resources.ProfilesPage_Yes}");

                    await result; // sync call

                    switch (result.Result)
                    {
                        case ContentDialogResult.Primary:
                            exists = false;
                            break;
                        default:
                            exists = true;
                            break;
                    }
                }

                if (!exists)
                    ProfileManager.UpdateOrCreateProfile(profile, UpdateSource.Creation);
            }
            catch (Exception ex)
            {
                LogManager.LogError(ex.Message);
            }
    }

    private void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        if (!selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        // TODO: MOVE ME TO LAYOUT !
        switch (((GyroActions)currentAction).MotionInput)
        {
            default:
            case MotionInput.JoystickCamera:
            case MotionInput.PlayerSpace:
                page0.SetProfile();
                MainWindow.NavView_Navigate(page0);
                break;
            case MotionInput.JoystickSteering:
                page1.SetProfile();
                MainWindow.NavView_Navigate(page1);
                break;
        }
    }

    private void PowerProfileManager_Deleted(PowerProfile powerProfile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
                if (item is not Button)
                    continue;

                // get power profile
                var parent = (Button)item;
                if (parent.Tag is not PowerProfile)
                    continue;

                PowerProfile pr = (PowerProfile)parent.Tag;

                bool isCurrent = pr.Guid == powerProfile.Guid;
                if (isCurrent)
                {
                    idx = ProfileStack.Children.IndexOf(parent);
                    break;
                }
            }

            if (idx != -1)
            {
                // remove profile
                ProfileStack.Children.RemoveAt(idx);

                // remove separator
                if (idx >= ProfileStack.Children.Count)
                    idx = ProfileStack.Children.Count - 1;
                ProfileStack.Children.RemoveAt(idx);
            }
        });
    }

    private void PowerProfileManager_Updated(PowerProfile powerProfile, UpdateSource source)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            int idx = -1;
            foreach (var item in ProfileStack.Children)
            {
                if (item is not Button)
                    continue;

                // get power profile
                var parent = (Button)item;
                if (parent.Tag is not PowerProfile)
                    continue;

                PowerProfile pr = (PowerProfile)parent.Tag;

                bool isCurrent = pr.Guid == powerProfile.Guid;
                if (isCurrent)
                {
                    idx = ProfileStack.Children.IndexOf(parent);
                    break;
                }
            }

            if (idx != -1)
            {
                // found it
                return;
            }
            else
            {
                // draw UI elements
                powerProfile.DrawUI(this);

                idx = ProfileStack.Children.Count;
                if (idx != 0)
                {
                    // Create a separator
                    Separator separator = new Separator();
                    separator.Margin = new Thickness(-16, 0, -16, 0);
                    separator.BorderBrush = (Brush)FindResource("SystemControlBackgroundChromeMediumBrush");
                    separator.BorderThickness = new Thickness(0, 1, 0, 0);
                    ProfileStack.Children.Add(separator);
                }

                Button button = powerProfile.GetButton(this);
                button.Click += (sender, e) => PowerProfile_Clicked(powerProfile);

                RadioButton radioButton = powerProfile.GetRadioButton(this);
                radioButton.Checked += (sender, e) => PowerProfile_Selected(powerProfile);

                // add new entry
                ProfileStack.Children.Add(button);
            }
        });
    }

    private void PowerProfile_Clicked(PowerProfile powerProfile)
    {
        RadioButton radioButton = powerProfile.GetRadioButton(this);
        if (radioButton.IsMouseOver)
            return;

        MainWindow.performancePage.SelectionChanged(powerProfile.Guid);
        MainWindow.GetCurrent().NavView_Navigate("PerformancePage");
    }

    private void PowerProfile_Selected(PowerProfile powerProfile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update UI
            SelectedPowerProfileName.Text = powerProfile.Name;

            // update profile
            selectedProfile.PowerProfile = powerProfile.Guid;
            UpdateProfile();
        });
    }

    private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Profiles.SelectedItem is null)
            return;

        // if an update is pending, cut it short, it will disturb profile selection though
        if (UpdateTimer.Enabled)
        {
            UpdateTimer.Stop();
            SubmitProfile();
        }

        selectedProfile = (Profile)cB_Profiles.SelectedItem;

        UpdateUI();
    }

    private void UpdateMotionControlsVisibility()
    {
        bool MotionMapped = false;
        if (selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
            if (action is not null && action.ActionType != ActionType.Disabled)
                MotionMapped = true;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            MotionControlAdditional.IsEnabled = MotionMapped ? true : false;
        });
    }

    private void UpdateUI()
    {
        if (selectedProfile is null)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            using (new ScopedLock(updateLock))
            {
                // disable delete button if is default profile or running
                b_DeleteProfile.IsEnabled = !selectedProfile.ErrorCode.HasFlag(ProfileErrorCode.Default & ProfileErrorCode.Running);
                // prevent user from renaming default profile
                tB_ProfileName.IsEnabled = !selectedProfile.Default;
                // prevent user from disabling default profile
                Toggle_EnableProfile.IsEnabled = !selectedProfile.Default;
                // prevent user from disabling default profile layout
                Toggle_ControllerLayout.IsEnabled = !selectedProfile.Default;
                // prevent user from using Wrapper on default profile
                cB_Wrapper.IsEnabled = !selectedProfile.Default;

                // Profile info
                tB_ProfileName.Text = selectedProfile.Name;
                tB_ProfilePath.Text = selectedProfile.Path;
                Toggle_EnableProfile.IsOn = selectedProfile.Enabled;

                // Global settings
                cB_Whitelist.IsChecked = selectedProfile.Whitelisted;
                cB_Wrapper.SelectedIndex = (int)selectedProfile.XInputPlus;

                // Motion control settings
                tb_ProfileGyroValue.Value = selectedProfile.GyrometerMultiplier;
                tb_ProfileAcceleroValue.Value = selectedProfile.AccelerometerMultiplier;

                cB_GyroSteering.SelectedIndex = selectedProfile.SteeringAxis;
                cB_InvertHorizontal.IsChecked = selectedProfile.MotionInvertHorizontal;
                cB_InvertVertical.IsChecked = selectedProfile.MotionInvertVertical;

                UpdateMotionControlsVisibility();

                // RSR
                RSRToggle.IsOn = selectedProfile.RSREnabled;
                RSRSlider.Value = selectedProfile.RSRSharpness;

                // Framerate limit
                FramerateToggle.IsOn = selectedProfile.FramerateEnabled;
                FramerateSlider.Value = selectedProfile.FramerateValue;

                // Layout settings
                Toggle_ControllerLayout.IsOn = selectedProfile.LayoutEnabled;

                // power profile
                PowerProfile powerProfile = PowerProfileManager.GetProfile(selectedProfile.PowerProfile);
                powerProfile.Check(this);

                // display warnings
                WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(selectedProfile.ErrorCode);

                switch (selectedProfile.ErrorCode)
                {
                    default:
                    case ProfileErrorCode.None:
                        WarningBorder.Visibility = Visibility.Collapsed;
                        cB_Whitelist.IsEnabled = true;

                        // wrapper
                        cB_Wrapper_Injection.IsEnabled = true;
                        cB_Wrapper_Redirection.IsEnabled = true;
                        break;

                    case ProfileErrorCode.Running:              // application is running
                    case ProfileErrorCode.MissingExecutable:    // profile has no executable
                    case ProfileErrorCode.MissingPath:          // profile has no path
                    case ProfileErrorCode.Default:              // profile is default
                        WarningBorder.Visibility = Visibility.Visible;
                        cB_Whitelist.IsEnabled = false;
                        cB_Wrapper.IsEnabled = false;

                        // wrapper
                        cB_Wrapper_Injection.IsEnabled = false;
                        cB_Wrapper_Redirection.IsEnabled = false;
                        break;

                    case ProfileErrorCode.MissingPermission:
                        WarningBorder.Visibility = Visibility.Visible;
                        cB_Whitelist.IsEnabled = true;

                        // wrapper
                        cB_Wrapper_Injection.IsEnabled = true;
                        cB_Wrapper_Redirection.IsEnabled = false;
                        break;
                }
            }
        });
    }

    private async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        var result = Dialog.ShowAsync(
            $"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{selectedProfile.Name}\"?",
            $"{Properties.Resources.ProfilesPage_AreYouSureDelete2}",
            ContentDialogButton.Primary,
            $"{Properties.Resources.ProfilesPage_Cancel}",
            $"{Properties.Resources.ProfilesPage_Delete}");
        await result; // sync call

        switch (result.Result)
        {
            case ContentDialogResult.Primary:
                ProfileManager.DeleteProfile(selectedProfile);
                cB_Profiles.SelectedIndex = 0;
                break;
        }
    }

    private void cB_Whitelist_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.Whitelisted = (bool)cB_Whitelist.IsChecked;
        UpdateProfile();
    }

    private void cB_Wrapper_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_Wrapper.SelectedIndex == -1)
            return;

        if (selectedProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.XInputPlus = (XInputPlusMethod)cB_Wrapper.SelectedIndex;

        switch (selectedProfile.XInputPlus)
        {
            case XInputPlusMethod.Injection:
                cB_Whitelist.IsChecked = true;
                break;
            case XInputPlusMethod.Redirection:
                cB_Whitelist.IsChecked = false;
                break;
        }

        UpdateProfile();
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void Toggle_EnableProfile_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.Enabled = Toggle_EnableProfile.IsOn;
        UpdateProfile();
    }

    private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (FramerateToggle.IsOn)
            {
                FramerateSlider_ValueChanged(null, null);
            }
            else
            {
                foreach (Control control in FramerateModeGrid.Children)
                {
                    if (control is not Label)
                        continue;

                    control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                }
            }
        });

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.FramerateEnabled = FramerateToggle.IsOn;
        UpdateProfile();
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (Control control in FramerateModeGrid.Children)
            {
                if (control is not Label)
                    continue;

                control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            }

            Label label = (Label)FramerateModeGrid.Children[value];
            label.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
        });

        if (!FramerateSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.FramerateValue = (int)FramerateSlider.Value;
        UpdateProfile();
    }

    private void ControllerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // prepare layout editor
        LayoutTemplate layoutTemplate = new(selectedProfile.Layout)
        {
            Name = selectedProfile.LayoutTitle,
            Description = "Your modified layout for this executable.",
            Author = Environment.UserName,
            Executable = selectedProfile.Executable,
            Product = selectedProfile.Name,
        };
        layoutTemplate.Updated += Template_Updated;

        MainWindow.layoutPage.UpdateLayoutTemplate(layoutTemplate);
        MainWindow.NavView_Navigate(MainWindow.layoutPage);
    }

    private void Template_Updated(LayoutTemplate layoutTemplate)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            selectedProfile.LayoutTitle = layoutTemplate.Name;
        });

        selectedProfile.Layout = layoutTemplate.Layout;
        UpdateMotionControlsVisibility();

        UpdateProfile();
    }

    #region UI

    private void ProfileApplied(Profile profile, UpdateSource source)
    {
        if (profile.Default)
            return;

        ProfileUpdated(profile, source, true);
    }

    public void ProfileUpdated(Profile profile, UpdateSource source, bool isCurrent)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var idx = -1;
            foreach (Profile pr in cB_Profiles.Items)
            {
                var isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                if (isCurrent)
                {
                    idx = cB_Profiles.Items.IndexOf(pr);
                    break;
                }
            }

            if (idx != -1)
                cB_Profiles.Items[idx] = profile;
            else
                cB_Profiles.Items.Add(profile);

            cB_Profiles.Items.Refresh();

            cB_Profiles.SelectedItem = profile;
        });
    }

    public void ProfileDeleted(Profile profile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var idx = -1;
            foreach (Profile pr in cB_Profiles.Items)
            {
                var isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                if (isCurrent)
                {
                    idx = cB_Profiles.Items.IndexOf(pr);
                    break;
                }
            }

            cB_Profiles.Items.RemoveAt(idx);
        });
    }

    private void ProfileManagerLoaded()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { cB_Profiles.SelectedItem = ProfileManager.GetDefault(); });
    }

    #endregion

    private void tB_ProfileName_TextChanged(object sender, TextChangedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.Name = tB_ProfileName.Text;
        UpdateProfile();
    }

    private void tb_ProfileGyroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileGyroValue.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
        UpdateProfile();
    }

    private void tb_ProfileAcceleroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileAcceleroValue.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;
        UpdateProfile();
    }

    private void cB_GyroSteering_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_GyroSteering.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.SteeringAxis = cB_GyroSteering.SelectedIndex;
        UpdateProfile();
    }

    private void cB_InvertHorizontal_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;
        UpdateProfile();
    }

    private void cB_InvertVertical_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
        UpdateProfile();
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        // Layout settings
        switch (selectedProfile.Default)
        {
            case true:
                selectedProfile.LayoutEnabled = true;
                break;
            case false:
                selectedProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
                break;
        }
        UpdateProfile();
    }

    public static void UpdateProfile()
    {
        if (UpdateTimer is not null)
        {
            UpdateTimer.Stop();
            UpdateTimer.Start();
        }
    }

    public void SubmitProfile(UpdateSource source = UpdateSource.ProfilesPage)
    {
        if (selectedProfile is null)
            return;

        ProfileManager.UpdateOrCreateProfile(selectedProfile, source);
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.RSREnabled = RSRToggle.IsOn;
        UpdateProfile();
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!RSRSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.RSRSharpness = (int)RSRSlider.Value;
        UpdateProfile();
    }
}