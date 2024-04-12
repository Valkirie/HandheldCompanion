using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Pages.Profiles;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using static HandheldCompanion.Utils.XInputPlusUtils;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    // when set on start cannot be null anymore
    public static Profile selectedProfile;
    private static Profile selectedMainProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");

    private CrossThreadLock profileLock = new();
    private CrossThreadLock graphicLock = new();

    private const int UpdateInterval = 500;
    private static Timer UpdateTimer;

    public ProfilesPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public ProfilesPage()
    {
        InitializeComponent();

        // manage events
        ProfileManager.Deleted += ProfileDeleted;
        ProfileManager.Updated += ProfileUpdated;
        ProfileManager.Applied += ProfileApplied;
        ProfileManager.Initialized += ProfileManagerLoaded;
        PowerProfileManager.Updated += PowerProfileManager_Updated;
        PowerProfileManager.Deleted += PowerProfileManager_Deleted;
        MultimediaManager.Initialized += MultimediaManager_Initialized;
        MultimediaManager.DisplaySettingsChanged += MultimediaManager_DisplaySettingsChanged;
        PlatformManager.RTSS.Updated += RTSS_Updated;
        GPUManager.Hooked += GPUManager_Hooked;
        GPUManager.Unhooked += GPUManager_Unhooked;

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        // auto-sort
        cB_Profiles.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Descending));

        // force call
        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void MultimediaManager_Initialized()
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            DesktopScreen desktopScreen = MultimediaManager.GetDesktopScreen();
            desktopScreen.screenDividers.ForEach(d => IntegerScalingComboBox.Items.Add(d));
        });
    }

    private void GPUManager_Hooked(GPU GPU)
    {
        bool HasRSRSupport = false;
        if (GPU is AMDGPU amdGPU)
        {
            amdGPU.RSRStateChanged += OnRSRStateChanged;
            HasRSRSupport = amdGPU.HasRSRSupport();
        }

        GPU.IntegerScalingChanged += OnIntegerScalingChanged;
        GPU.GPUScalingChanged += OnGPUScalingChanged;

        bool HasScalingModeSupport = GPU.HasScalingModeSupport();
        bool HasIntegerScalingSupport = GPU.HasIntegerScalingSupport();
        bool HasGPUScalingSupport = GPU.HasGPUScalingSupport();
        bool IsGPUScalingEnabled = GPU.GetGPUScaling();

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            // GPU-specific settings
            StackProfileRSR.Visibility = GPU is AMDGPU ? Visibility.Visible : Visibility.Collapsed;

            StackProfileRSR.IsEnabled = HasGPUScalingSupport && IsGPUScalingEnabled && HasRSRSupport;
            StackProfileIS.IsEnabled = HasGPUScalingSupport && IsGPUScalingEnabled && HasIntegerScalingSupport;
            GPUScalingToggle.IsEnabled = HasGPUScalingSupport;
            GPUScalingComboBox.IsEnabled = HasGPUScalingSupport && HasScalingModeSupport;
        });
    }

    private void GPUManager_Unhooked(GPU GPU)
    {
        if (GPU is AMDGPU amdGPU)
            amdGPU.RSRStateChanged -= OnRSRStateChanged;

        GPU.IntegerScalingChanged -= OnIntegerScalingChanged;
        GPU.GPUScalingChanged -= OnGPUScalingChanged;

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileRSR.IsEnabled = false;
            StackProfileIS.IsEnabled = false;
            GPUScalingToggle.IsEnabled = false;
            GPUScalingComboBox.IsEnabled = false;
        });
    }

    private void OnRSRStateChanged(bool Supported, bool Enabled, int Sharpness)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileRSR.IsEnabled = Supported;
        });
    }

    private void OnGPUScalingChanged(bool Supported, bool Enabled, int Mode)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            GPUScalingToggle.IsEnabled = Supported;
            StackProfileRIS.IsEnabled = Supported; // check if processor is AMD should be enough
            StackProfileRSR.IsEnabled = Supported;
            StackProfileIS.IsEnabled = Supported;
        });
    }

    private void OnIntegerScalingChanged(bool Supported, bool Enabled)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            StackProfileIS.IsEnabled = Supported;
        });
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    var Processor = PerformanceManager.GetProcessor();
                    StackProfileFramerate.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    // StackProfileFramerate.IsEnabled = false;
                    // StackProfileAutoTDP.IsEnabled = false;
                    break;
            }
        });
    }

    private void MultimediaManager_DisplaySettingsChanged(DesktopScreen desktopScreen, ScreenResolution resolution)
    {
        List<ScreenFramelimit> frameLimits = desktopScreen.GetFramelimits();

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            cB_Framerate.Items.Clear();

            foreach (ScreenFramelimit frameLimit in frameLimits)
                cB_Framerate.Items.Add(frameLimit);

            cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);
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

                Profile profile = new Profile(path);
                Layout toCloneLayout = ProfileManager.GetProfileWithDefaultLayout()?.Layout ?? LayoutTemplate.DefaultLayout.Layout;
                profile.Layout = toCloneLayout.Clone() as Layout;
                profile.LayoutTitle = LayoutTemplate.DefaultLayout.Name;

                var exists = false;

                // check on path rather than profile
                if (ProfileManager.Contains(path))
                {
                    Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
                    {
                        Title = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite1, profile.Name),
                        Content = string.Format(Properties.Resources.ProfilesPage_AreYouSureOverwrite2, profile.Name),
                        CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
                        PrimaryButtonText = Properties.Resources.ProfilesPage_Yes
                    }.ShowAsync();

                    await dialogTask; // sync call

                    switch (dialogTask.Result)
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
        Application.Current.Dispatcher.Invoke(() =>
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
        Application.Current.Dispatcher.Invoke(() =>
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
                if (button is not null)
                    button.Click += (sender, e) => PowerProfile_Clicked(powerProfile);

                RadioButton radioButton = powerProfile.GetRadioButton(this);
                if (radioButton is not null)
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

        MainWindow.performancePage.SelectionChanged(powerProfile);
        MainWindow.GetCurrent().NavView_Navigate("PerformancePage");
    }

    private void PowerProfile_Selected(PowerProfile powerProfile)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
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

        selectedMainProfile = (Profile)cB_Profiles.SelectedItem;
        UpdateSubProfiles();
    }

    private void UpdateMotionControlsVisibility()
    {
        bool MotionMapped = false;
        if (selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions action))
            if (action is not null && action.actionType != ActionType.Disabled)
                MotionMapped = true;

        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            MotionControlAdditional.IsEnabled = MotionMapped ? true : false;
        });
    }

    private void UpdateUI()
    {
        if (selectedProfile is null)
            return;

        if (profileLock.TryEnter())
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // disable delete button if is default profile or any sub profile is running
                    b_DeleteProfile.IsEnabled = !selectedProfile.ErrorCode.HasFlag(ProfileErrorCode.Default & ProfileErrorCode.Running); //TODO consider sub profiles pertaining to this main profile is running
                                                                                                                                         // prevent user from renaming default profile
                    b_ProfileRename.IsEnabled = !selectedMainProfile.Default;
                    // prevent user from disabling default profile
                    Toggle_EnableProfile.IsEnabled = !selectedProfile.Default;
                    // prevent user from disabling default profile layout
                    Toggle_ControllerLayout.IsEnabled = !selectedProfile.Default;
                    // prevent user from using Wrapper on default profile
                    cB_Wrapper.IsEnabled = !selectedProfile.Default;
                    UseFullscreenOptimizations.IsEnabled = !selectedProfile.Default;
                    UseHighDPIAwareness.IsEnabled = !selectedProfile.Default;

                    // sub profiles
                    b_SubProfileCreate.IsEnabled = !selectedMainProfile.Default;

                    // enable delete and rename if not default sub profile
                    if (cb_SubProfilePicker.SelectedIndex == 0) // main profile
                    {
                        b_SubProfileDelete.IsEnabled = false;
                        b_SubProfileRename.IsEnabled = false;
                    }
                    else // actual sub profile
                    {
                        b_SubProfileDelete.IsEnabled = true;
                        b_SubProfileRename.IsEnabled = true;
                    }

                    // Profile info
                    tB_ProfileName.Text = selectedMainProfile.Name;
                    tB_ProfilePath.Text = selectedProfile.Path;
                    Toggle_EnableProfile.IsOn = selectedProfile.Enabled;

                    // Global settings
                    cB_Whitelist.IsChecked = selectedProfile.Whitelisted;
                    cB_Wrapper.SelectedIndex = (int)selectedProfile.XInputPlus;

                    // Emulated controller assigned to the profile
                    cB_EmulatedController.IsEnabled = !selectedProfile.Default; // if default profile, disable combobox
                    cB_EmulatedController.SelectedIndex = new Func<int>(() =>
                    {
                        if (selectedProfile.Default) // Default profile always shows default, but keeps track of default controller internally
                            return 0;
                        else if (selectedProfile.HID == HIDmode.Xbox360Controller)
                            return 1;
                        else if (selectedProfile.HID == HIDmode.DualShock4Controller)
                            return 2;
                        else
                            return 0; // Current or not assigned
                    })();

                    // Motion control settings
                    tb_ProfileGyroValue.Value = selectedProfile.GyrometerMultiplier;
                    tb_ProfileAcceleroValue.Value = selectedProfile.AccelerometerMultiplier;

                    cB_GyroSteering.SelectedIndex = (byte)selectedProfile.SteeringAxis;
                    cB_InvertHorizontal.IsChecked = selectedProfile.MotionInvertHorizontal;
                    cB_InvertVertical.IsChecked = selectedProfile.MotionInvertVertical;

                    UpdateMotionControlsVisibility();

                    // Framerate limit
                    DesktopScreen? desktopScreen = MultimediaManager.GetDesktopScreen();
                    if (desktopScreen is not null)
                        cB_Framerate.SelectedItem = desktopScreen.GetClosest(selectedProfile.FramerateValue);

                    // GPU Scaling
                    GPUScalingToggle.IsOn = selectedProfile.GPUScaling;
                    GPUScalingComboBox.SelectedIndex = selectedProfile.ScalingMode;

                    // RSR
                    RSRToggle.IsOn = selectedProfile.RSREnabled;
                    RSRSlider.Value = selectedProfile.RSRSharpness;

                    // Integer Scaling
                    IntegerScalingToggle.IsOn = selectedProfile.IntegerScalingEnabled;

                    if (desktopScreen is not null)
                        IntegerScalingComboBox.SelectedItem = desktopScreen.screenDividers.FirstOrDefault(d => d.divider == selectedProfile.IntegerScalingDivider);

                    // RIS
                    RISToggle.IsOn = selectedProfile.RISEnabled;
                    RISSlider.Value = selectedProfile.RISSharpness;

                    // Compatibility settings
                    UseFullscreenOptimizations.IsOn = selectedProfile.FullScreenOptimization;
                    UseHighDPIAwareness.IsOn = selectedProfile.HighDPIAware;

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

                    // update dropdown lists
                    cB_Profiles.Items.Refresh();
                    cb_SubProfilePicker.Items.Refresh();
                });
            }
            finally
            {
                profileLock.Exit();
            }
        }
    }

    private void UpdateSubProfiles()
    {
        if (selectedMainProfile is null)
            return;

        var ind = 0; // default or main profile itself

        // add main profile as first subprofile
        cb_SubProfilePicker.Items.Clear();
        cb_SubProfilePicker.Items.Add(selectedMainProfile);

        // if main profile is not default, occupy sub profiles dropdown list
        if (!selectedMainProfile.Default)
        {
            foreach (Profile subprofile in ProfileManager.GetSubProfilesFromPath(selectedMainProfile.Path, false))
            {
                cb_SubProfilePicker.Items.Add(subprofile);

                // select sub profile if it's favorite for main profile
                if (subprofile.IsFavoriteSubProfile)
                    ind = cb_SubProfilePicker.Items.IndexOf(subprofile);
            }
        }

        // refresh sub profiles dropdown
        cb_SubProfilePicker.Items.Refresh();

        // set subprofile to be applied
        cb_SubProfilePicker.SelectedIndex = ind;

        // update UI elements
        UpdateUI();
    }

    private async void b_DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = $"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{selectedMainProfile.Name}\"?",
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ProfileManager.DeleteProfile(selectedMainProfile);
                cB_Profiles.SelectedIndex = 0;
                break;
        }
    }

    private void cB_Whitelist_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
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

        // prevent update loop
        if (profileLock.IsEntered())
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
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.Enabled = Toggle_EnableProfile.IsOn;
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
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
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
        // self call - update ui and return
        switch (source)
        {
            case UpdateSource.ProfilesPage:
            case UpdateSource.ProfilesPageUpdateOnly:
                return;
            case UpdateSource.QuickProfilesPage:
                {
                    isCurrent = selectedProfile.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
                    if (!isCurrent) return;
                }
                break;
        }

        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            var idx = -1;
            if (!profile.IsSubProfile && cb_SubProfilePicker.Items.IndexOf(profile) != 0)
            {
                foreach (Profile pr in cB_Profiles.Items)
                {
                    bool isCurrent = pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase);
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
            }

            else if (!profile.IsFavoriteSubProfile)
                cB_Profiles.SelectedItem = profile;

            else // TODO updateUI to show main & sub profile selected
            {
                Profile mainProfile = ProfileManager.GetProfileForSubProfile(profile);
                cB_Profiles.SelectedItem = mainProfile;
            }

            UpdateSubProfiles(); // TODO check
        });
    }

    public void ProfileDeleted(Profile profile)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            int prevIdx = cB_Profiles.SelectedIndex;

            if (!profile.IsSubProfile)
            {
                // Profiles
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

                if (idx == -1)
                    return;

                cB_Profiles.Items.RemoveAt(idx);

                if (prevIdx == idx)
                    cB_Profiles.SelectedIndex = 0;
            }
            else
            {
                var idx = -1;
                foreach (Profile pr in cb_SubProfilePicker.Items)
                {
                    // sub profiles
                    var isCurrent = profile.Guid == pr.Guid;
                    if (isCurrent)
                    {
                        idx = cb_SubProfilePicker.Items.IndexOf(pr);
                        break;
                    }
                }

                if (idx == -1)
                    return;

                cb_SubProfilePicker.Items.RemoveAt(idx);
                cb_SubProfilePicker.SelectedIndex = 0;
            }
        });
    }

    private void ProfileManagerLoaded()
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() => { cB_Profiles.SelectedItem = ProfileManager.GetDefault(); });
    }

    #endregion

    private void tb_ProfileGyroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileGyroValue.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
        UpdateProfile();
    }

    private void tb_ProfileAcceleroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileAcceleroValue.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;
        UpdateProfile();
    }

    private void cB_GyroSteering_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_GyroSteering.SelectedIndex == -1)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.SteeringAxis = (SteeringAxis)cB_GyroSteering.SelectedIndex;
        UpdateProfile();
    }

    private void cB_InvertHorizontal_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;
        UpdateProfile();
    }

    private void cB_InvertVertical_Checked(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
        UpdateProfile();
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        // prevent update loop
        if (profileLock.IsEntered())
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

        LogManager.LogInformation($"Submitting profile in ProfilesPage: {selectedProfile} - is Sub Profile? {selectedProfile.IsSubProfile}");

        switch (source)
        {
            case UpdateSource.ProfilesPageUpdateOnly: // when renaming main profile, update main profile only but don't apply it
                ProfileManager.UpdateOrCreateProfile(selectedMainProfile, source);
                break;
            default:
                ProfileManager.UpdateOrCreateProfile(selectedProfile, source);
                break;
        }
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.RadeonSuperResolution, RSRToggle.IsOn);
        UpdateProfile();
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (!RSRSlider.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        selectedProfile.RSRSharpness = (int)RSRSlider.Value;
        UpdateProfile();
    }

    private void RISToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.RadeonImageSharpening, RISToggle.IsOn);
        UpdateProfile();
    }

    private void RISSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (!RISSlider.IsInitialized)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        selectedProfile.RISSharpness = (int)RISSlider.Value;
        UpdateProfile();
    }

    private void IntegerScalingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.IntegerScaling, IntegerScalingToggle.IsOn);
        UpdateProfile();
    }

    private void IntegerScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IntegerScalingComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        var divider = 1;
        if (IntegerScalingComboBox.SelectedItem is ScreenDivider screenDivider)
        {
            divider = screenDivider.divider;
        }

        selectedProfile.IntegerScalingDivider = divider;
        UpdateProfile();
    }

    private void GPUScalingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GPUScalingComboBox.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        int selectedIndex = GPUScalingComboBox.SelectedIndex;
        // RSR does not work with ScalingMode.Center
        if (selectedProfile.RSREnabled && selectedIndex == 2)
        {
            selectedProfile.ScalingMode = 1;
            GPUScalingComboBox.SelectedIndex = 1;
        }
        else
        {
            selectedProfile.ScalingMode = GPUScalingComboBox.SelectedIndex;
        }

        UpdateProfile();
    }

    private void GPUScaling_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered() || graphicLock.IsEntered())
            return;

        UpdateGraphicsSettings(UpdateGraphicsSettingsSource.GPUScaling, GPUScalingToggle.IsOn);
        UpdateProfile();
    }

    private void cB_EmulatedController_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (selectedProfile is null)
            return;
        
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        ComboBoxItem selectedEmulatedController = (ComboBoxItem)cB_EmulatedController.SelectedItem;

        if (selectedEmulatedController is null || selectedEmulatedController.Tag is null)
            return;

        HIDmode HIDmode = (HIDmode)int.Parse(selectedEmulatedController.Tag.ToString());
        selectedProfile.HID = HIDmode;

        UpdateProfile();
    }

    private void cb_SubProfilePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cb_SubProfilePicker.SelectedIndex == -1)
            return;
        
        // prevent update loop
        if (profileLock.IsEntered())
            return;

        LogManager.LogInformation($"Subprofile changed in ProfilesPage - ind: {cb_SubProfilePicker.SelectedIndex} - {cb_SubProfilePicker.SelectedItem}");

        // selected sub profile
        if (selectedProfile != cb_SubProfilePicker.SelectedItem)
        {
            selectedProfile = (Profile)cb_SubProfilePicker.SelectedItem;
            UpdateProfile();
        }

        UpdateUI();
    }

    private void b_SubProfileCreate_Click(object sender, RoutedEventArgs e)
    {
        // create a new sub profile matching the original profile's settings
        Profile newSubProfile = (Profile)selectedProfile.Clone();
        newSubProfile.Name = "(New Sub Profile)";
        newSubProfile.Guid = Guid.NewGuid(); // must be unique
        newSubProfile.IsSubProfile = true;
        newSubProfile.IsFavoriteSubProfile = true;
        ProfileManager.UpdateOrCreateProfile(newSubProfile);
        UpdateSubProfiles();
    }

    private async void b_SubProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        // return if original profile or nothing is selected
        if (cb_SubProfilePicker.SelectedIndex <= 0)
            return;

        // get selected subprofile from dropdown
        Profile subProfile = (Profile)cb_SubProfilePicker.SelectedItem;

        // user confirmation
        Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
        {
            Title = $"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{subProfile.Name}\"?",
            Content = Properties.Resources.ProfilesPage_AreYouSureDelete2,
            CloseButtonText = Properties.Resources.ProfilesPage_Cancel,
            PrimaryButtonText = Properties.Resources.ProfilesPage_Delete
        }.ShowAsync();

        await dialogTask; // sync call

        switch (dialogTask.Result)
        {
            case ContentDialogResult.Primary:
                ProfileManager.DeleteSubProfile(subProfile);
                break;
        }
    }

    private void b_SubProfileRename_Click(object sender, RoutedEventArgs e)
    {
        tb_SubProfileName.Text = selectedProfile.Name;
        SubProfileRenameDialog.ShowAsync();
    }

    private void SubProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (selectedProfile is null)
            return;

        // update subprofile name
        selectedProfile.Name = tb_SubProfileName.Text;

        // serialize subprofile
        SubmitProfile();

        UpdateSubProfiles();
    }

    private void SubProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // restore subprofile rename dialog
        tb_SubProfileName.Text = selectedProfile.Name;
    }

    private void ProfileRenameDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // restore profile name textbox
        tB_ProfileName.Text = selectedMainProfile.Name;
    }

    private void ProfileRenameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // change main profile name
        selectedMainProfile.Name = tB_ProfileName.Text;

        // change it in 
        int ind = cB_Profiles.Items.IndexOf(selectedMainProfile);
        cB_Profiles.Items[ind] = selectedMainProfile;
        cB_Profiles.Items.Refresh();
        cB_Profiles.SelectedIndex = ind;

        SubmitProfile(UpdateSource.ProfilesPageUpdateOnly);
    }

    private void b_ProfileRename_Click(object sender, RoutedEventArgs e)
    {
        tB_ProfileName.Text = selectedMainProfile.Name;
        ProfileRenameDialog.ShowAsync();
    }

    private void cB_Framerate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Framerate.SelectedIndex == -1 || selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        if (cB_Framerate.SelectedItem is ScreenFramelimit screenFramelimit)
        {
            selectedProfile.FramerateValue = screenFramelimit.limit;
            UpdateProfile();
        }
    }

    private enum UpdateGraphicsSettingsSource
    {
        GPUScaling,
        RadeonSuperResolution,
        RadeonImageSharpening,
        IntegerScaling
    }

    private void UpdateGraphicsSettings(UpdateGraphicsSettingsSource source, bool isEnabled)
    {
        if (graphicLock.TryEnter())
        {
            try
            {
                switch (source)
                {
                    case UpdateGraphicsSettingsSource.GPUScaling:
                        {
                            selectedProfile.GPUScaling = isEnabled;
                            if (!isEnabled)
                            {
                                selectedProfile.RSREnabled = false;
                                selectedProfile.IntegerScalingEnabled = false;

                                RSRToggle.IsOn = false;
                                IntegerScalingToggle.IsOn = false;
                            }
                        }
                        break;
                    // RSR is incompatible with RIS and IS
                    case UpdateGraphicsSettingsSource.RadeonSuperResolution:
                        {
                            selectedProfile.RSREnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RISEnabled = false;
                                selectedProfile.IntegerScalingEnabled = false;

                                RISToggle.IsOn = false;
                                IntegerScalingToggle.IsOn = false;

                                // RSR does not support ScalingMode.Center
                                if (selectedProfile.ScalingMode == 2)
                                {
                                    selectedProfile.ScalingMode = 1;
                                    GPUScalingComboBox.SelectedIndex = 1;
                                }
                            }
                        }
                        break;
                    // Image Sharpening is incompatible with RSR
                    case UpdateGraphicsSettingsSource.RadeonImageSharpening:
                        {
                            selectedProfile.RISEnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RSREnabled = false;

                                RSRToggle.IsOn = false;
                            }
                        }
                        break;

                    // Integer Scaling is incompatible with RSR
                    case UpdateGraphicsSettingsSource.IntegerScaling:
                        {
                            selectedProfile.IntegerScalingEnabled = isEnabled;
                            if (isEnabled)
                            {
                                selectedProfile.RSREnabled = false;

                                RSRToggle.IsOn = false;
                            }
                        }
                        break;
                }
            }
            finally
            {
                graphicLock.Exit();
            }
        }
    }

    private void UseFullscreenOptimizations_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.FullScreenOptimization = UseFullscreenOptimizations.IsOn;
        UpdateProfile();
    }

    private void UseHighDPIAwareness_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // prevent update loop
        if (profileLock.IsEntered())
            return;

        selectedProfile.HighDPIAware = UseHighDPIAwareness.IsOn;
        UpdateProfile();
    }
}