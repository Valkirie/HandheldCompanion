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
using System.Xml;
using static HandheldCompanion.Utils.XInputPlusUtils;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    public static Profile currentProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");

    private LockObject updateLock = new();
    private LockObject layoutLock = new();

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

        ProfileManager.Initialized += ProfileManagerLoaded;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        SystemManager.DisplaySettingsChanged += SystemManager_DisplaySettingsChanged;
        SystemManager.RSRStateChanged += SystemManager_RSRStateChanged;

        MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;

        // auto-sort
        cB_Profiles.Items.SortDescriptions.Add(new SortDescription("", ListSortDirection.Descending));

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

        // motherboard settings
        CPUCoreSlider.Maximum = MotherboardInfo.NumberOfCores;
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

    private void PerformanceManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            TDPToggle.IsEnabled = CanChangeTDP;
            AutoTDPToggle.IsEnabled = CanChangeGPU;

            GPUToggle.IsEnabled = CanChangeGPU;
        });
    }

    private void PerformanceManager_EPPChanged(uint EPP)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { EPPSlider.Value = EPP; });
    }

    public void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "ConfigurableTDPOverrideDown":
                    {
                        using (new ScopedLock(updateLock))
                        {
                            TDPSlider.Minimum = (double)value;
                        }
                    }
                    break;
                case "ConfigurableTDPOverrideUp":
                    {
                        using (new ScopedLock(updateLock))
                        {
                            TDPSlider.Maximum = (double)value;
                        }
                    }
                    break;
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

                var profile = new Profile(path);
                profile.Layout = LayoutTemplate.DefaultLayout.Layout.Clone() as Layout;
                profile.LayoutTitle = LayoutTemplate.DefaultLayout.Name;
                profile.TDPOverrideValues = MainWindow.CurrentDevice.nTDP;

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
                    ProfileManager.UpdateOrCreateProfile(profile, ProfileUpdateSource.Creation);
            }
            catch (Exception ex)
            {
                LogManager.LogError(ex.Message);
            }
    }

    private void b_AdditionalSettings_Click(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        if (!currentProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
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

    private void cB_Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Profiles.SelectedItem is null)
            return;

        // if an update is pending, execute it and stop timer
        if (UpdateTimer.Enabled)
        {
            UpdateTimer.Stop();
            SubmitProfile();
        }

        // update current profile
        var profile = (Profile)cB_Profiles.SelectedItem;
        currentProfile = profile.Clone() as Profile;

        DrawProfile();
    }

    private void DrawProfile()
    {
        if (currentProfile is null)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            using (new ScopedLock(updateLock))
            {
                // disable button if is default profile or application is running
                b_DeleteProfile.IsEnabled =
                    !currentProfile.ErrorCode.HasFlag(ProfileErrorCode.Default & ProfileErrorCode.Running);

                // prevent user from renaming default profile
                tB_ProfileName.IsEnabled = !currentProfile.Default;
                // prevent user from disabling default profile
                Toggle_EnableProfile.IsEnabled = !currentProfile.Default;
                // prevent user from disabling default profile layout
                Toggle_ControllerLayout.IsEnabled = !currentProfile.Default;
                // prevent user from using Wrapper on default profile
                cB_Wrapper.IsEnabled = !currentProfile.Default;

                // Profile info
                tB_ProfileName.Text = currentProfile.Name;
                tB_ProfilePath.Text = currentProfile.Path;
                Toggle_EnableProfile.IsOn = currentProfile.Enabled;

                // Global settings
                cB_Whitelist.IsChecked = currentProfile.Whitelisted;
                cB_Wrapper.SelectedIndex = (int)currentProfile.XInputPlus;

                // Motion control settings
                tb_ProfileGyroValue.Value = currentProfile.GyrometerMultiplier;
                tb_ProfileAcceleroValue.Value = currentProfile.AccelerometerMultiplier;

                cB_GyroSteering.SelectedIndex = currentProfile.SteeringAxis;
                cB_InvertHorizontal.IsChecked = currentProfile.MotionInvertHorizontal;
                cB_InvertVertical.IsChecked = currentProfile.MotionInvertVertical;

                // Sustained TDP settings (slow, stapm, long)
                TDPToggle.IsOn = currentProfile.TDPOverrideEnabled;
                var TDP = currentProfile.TDPOverrideValues is not null
                    ? currentProfile.TDPOverrideValues
                    : MainWindow.CurrentDevice.nTDP;
                TDPSlider.Value = TDP[(int)PowerType.Slow];

                // define slider(s) min and max values based on device specifications
                TDPSlider.Minimum = SettingsManager.GetInt("ConfigurableTDPOverrideDown");
                TDPSlider.Maximum = SettingsManager.GetInt("ConfigurableTDPOverrideUp");

                // Automatic TDP
                AutoTDPToggle.IsOn = currentProfile.AutoTDPEnabled;
                AutoTDPSlider.Value = (int)currentProfile.AutoTDPRequestedFPS;

                // EPP
                EPPToggle.IsOn = currentProfile.EPPOverrideEnabled;
                EPPSlider.Value = currentProfile.EPPOverrideValue;

                // RSR
                RSRToggle.IsOn = currentProfile.RSREnabled;
                RSRSlider.Value = currentProfile.RSRSharpness;

                // CPU Core Count
                CPUCoreToggle.IsOn = currentProfile.CPUCoreEnabled;
                CPUCoreSlider.Value = currentProfile.CPUCoreCount;

                // GPU Clock control
                GPUToggle.IsOn = currentProfile.GPUOverrideEnabled;
                GPUSlider.Value = currentProfile.GPUOverrideValue != 0 ? currentProfile.GPUOverrideValue : 255 * 50;

                // Framerate limit
                FramerateToggle.IsOn = currentProfile.FramerateEnabled;
                FramerateSlider.Value = currentProfile.FramerateValue;

                // Layout settings
                Toggle_ControllerLayout.IsOn = currentProfile.LayoutEnabled;


                // display warnings
                WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentProfile.ErrorCode);

                switch (currentProfile.ErrorCode)
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
        if (currentProfile is null)
            return;

        var result = Dialog.ShowAsync(
            $"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{currentProfile.Name}\"?",
            $"{Properties.Resources.ProfilesPage_AreYouSureDelete2}",
            ContentDialogButton.Primary,
            $"{Properties.Resources.ProfilesPage_Cancel}",
            $"{Properties.Resources.ProfilesPage_Delete}");
        await result; // sync call

        switch (result.Result)
        {
            case ContentDialogResult.Primary:
                ProfileManager.DeleteProfile(currentProfile);
                cB_Profiles.SelectedIndex = 0;
                break;
        }
    }

    private void cB_Whitelist_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.Whitelisted = (bool)cB_Whitelist.IsChecked;
        RequestUpdate();
    }

    private void cB_Wrapper_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (cB_Wrapper.SelectedIndex == -1)
            return;

        if (currentProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.XInputPlus = (XInputPlusMethod)cB_Wrapper.SelectedIndex;

        switch (currentProfile.XInputPlus)
        {
            case XInputPlusMethod.Injection:
                cB_Whitelist.IsChecked = true;
                break;
            case XInputPlusMethod.Redirection:
                cB_Whitelist.IsChecked = false;
                break;
        }

        RequestUpdate();
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

        currentProfile.Enabled = Toggle_EnableProfile.IsOn;
        RequestUpdate();
    }

    private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // TDP and AutoTDP are mutually exclusive
        var toggled = TDPToggle.IsOn;
        if (toggled)
            AutoTDPToggle.IsOn = false;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.TDPOverrideEnabled = TDPToggle.IsOn;
        RequestUpdate();
    }

    private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!TDPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.TDPOverrideValues = new double[3]
        {
            (int)TDPSlider.Value,
            (int)TDPSlider.Value,
            (int)TDPSlider.Value
        };
        RequestUpdate();
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
                    if (control.GetType() != typeof(Label))
                        continue;

                    control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
                }
            }
        });

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.FramerateEnabled = FramerateToggle.IsOn;
        RequestUpdate();
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (Control control in FramerateModeGrid.Children)
            {
                if (control.GetType() != typeof(Label))
                    continue;

                control.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");
            }

            Label Label = (Label)FramerateModeGrid.Children[value];
            Label.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
        });

        if (!FramerateSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.FramerateValue = (int)FramerateSlider.Value;
        RequestUpdate();
    }

    private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // TDP and AutoTDP are mutually exclusive
        var toggled = AutoTDPToggle.IsOn;
        if (toggled)
            TDPToggle.IsOn = false;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.AutoTDPEnabled = AutoTDPToggle.IsOn;
        RequestUpdate();
    }

    private void AutoTDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!AutoTDPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.AutoTDPRequestedFPS = (int)AutoTDPSlider.Value;
        RequestUpdate();
    }

    private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.GPUOverrideEnabled = GPUToggle.IsOn;
        RequestUpdate();
    }

    private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!GPUSlider.IsInitialized)
            return;

        if (currentProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.GPUOverrideValue = (int)GPUSlider.Value;
        RequestUpdate();
    }

    private void ControllerSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // prepare layout editor
        LayoutTemplate layoutTemplate = new(currentProfile.Layout)
        {
            Name = currentProfile.LayoutTitle,
            Description = "Your modified layout for this executable.",
            Author = Environment.UserName,
            Executable = currentProfile.Executable,
            Product = currentProfile.Name,
        };
        layoutTemplate.Updated += Template_Updated;

        // no lock needed here, layout itself will block any events back by its own lock
        MainWindow.layoutPage.UpdateLayout(layoutTemplate);
        MainWindow.NavView_Navigate(MainWindow.layoutPage);
    }

    private void Template_Updated(LayoutTemplate layoutTemplate)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            currentProfile.LayoutTitle = layoutTemplate.Name;
        });

        currentProfile.Layout = layoutTemplate.Layout;
        UpdateMotionControlsVisibility();

        RequestUpdate();
    }

    private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.EPPOverrideEnabled = EPPToggle.IsOn;
        RequestUpdate();
    }

    private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!EPPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.EPPOverrideValue = (uint)EPPSlider.Value;
        RequestUpdate();
    }

    #region UI

    private void ProfileApplied(Profile profile, ProfileUpdateSource source)
    {
        if (profile.Default)
            return;

        ProfileUpdated(profile, source, true);
    }

    public void ProfileUpdated(Profile profile, ProfileUpdateSource source, bool isCurrent)
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

        currentProfile.Name = tB_ProfileName.Text;
        RequestUpdate();
    }

    private void tb_ProfileGyroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileGyroValue.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
        RequestUpdate();
    }

    private void tb_ProfileAcceleroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileAcceleroValue.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;
        RequestUpdate();
    }

    private void cB_GyroSteering_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_GyroSteering.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.SteeringAxis = cB_GyroSteering.SelectedIndex;
        RequestUpdate();
    }

    private void cB_InvertHorizontal_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;
        RequestUpdate();
    }

    private void cB_InvertVertical_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
        RequestUpdate();
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        // Layout settings
        switch (currentProfile.Default)
        {
            case true:
                currentProfile.LayoutEnabled = true;
                break;
            case false:
                currentProfile.LayoutEnabled = Toggle_ControllerLayout.IsOn;
                break;
        }
        RequestUpdate();
    }

    public static void RequestUpdate()
    {
        if (UpdateTimer is not null)
        {
            UpdateTimer.Stop();
            UpdateTimer.Start();
        }
    }

    public void SubmitProfile(ProfileUpdateSource source = ProfileUpdateSource.ProfilesPage)
    {
        if (currentProfile is null)
            return;

        ProfileManager.UpdateOrCreateProfile(currentProfile, source);
    }

    private void RSRToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.RSREnabled = RSRToggle.IsOn;
        RequestUpdate();
    }

    private void RSRSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!RSRSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.RSRSharpness = (int)RSRSlider.Value;
        RequestUpdate();
    }

    private void CPUCoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.CPUCoreEnabled = CPUCoreToggle.IsOn;
        RequestUpdate();
    }

    private void CPUCoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!CPUCoreSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        currentProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
        RequestUpdate();
    }
}