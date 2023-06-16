using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using HandheldCompanion.Views.Pages.Profiles;
using Microsoft.Win32;
using Inkore.UI.WPF.Modern.Controls;
using Layout = ControllerCommon.Layout;
using Page = System.Windows.Controls.Page;
using System.Timers;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for Profiles.xaml
/// </summary>
public partial class ProfilesPage : Page
{
    public static Profile currentProfile;

    private readonly SettingsMode0 page0 = new("SettingsMode0");
    private readonly SettingsMode1 page1 = new("SettingsMode1");
    private Hotkey ProfilesPageHotkey = new(60);

    private bool profileLock;
    private bool layoutLock;

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

        SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

        HotkeysManager.HotkeyCreated += TriggerCreated;
        InputsManager.TriggerUpdated += TriggerUpdated;

        MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;

        // draw input modes
        foreach (var mode in (MotionInput[])Enum.GetValues(typeof(MotionInput)))
        {
            // create panel
            var panel = new SimpleStackPanel
                { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // create icon
            var icon = new FontIcon { Glyph = "" };

            switch (mode)
            {
                default:
                case MotionInput.PlayerSpace:
                    icon.Glyph = "\uF119";
                    break;
                case MotionInput.JoystickCamera:
                    icon.Glyph = "\uE714";
                    break;
                case MotionInput.AutoRollYawSwap:
                    icon.Glyph = "\uE7F8";
                    break;
                case MotionInput.JoystickSteering:
                    icon.Glyph = "\uEC47";
                    break;
            }

            if (icon.Glyph != "")
                panel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            panel.Children.Add(text);

            cB_Input.Items.Add(panel);
        }

        // draw output modes
        foreach (var mode in (MotionOutput[])Enum.GetValues(typeof(MotionOutput)))
        {
            // create panel
            var panel = new SimpleStackPanel
                { Spacing = 6, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // create icon
            var icon = new FontIcon { Glyph = "" };

            switch (mode)
            {
                default:
                case MotionOutput.RightStick:
                    icon.Glyph = "\uF109";
                    break;
                case MotionOutput.LeftStick:
                    icon.Glyph = "\uF108";
                    break;
            }

            if (icon.Glyph != "")
                panel.Children.Add(icon);

            // create textblock
            var description = EnumUtils.GetDescriptionFromEnumValue(mode);
            var text = new TextBlock { Text = description };
            panel.Children.Add(text);

            cB_Output.Items.Add(panel);
        }

        // auto-sort
        cB_Profiles.Items.SortDescriptions.Add(new SortDescription("", ListSortDirection.Descending));

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];
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
                    TDPSlider.Minimum = (double)value;
                    break;
                case "ConfigurableTDPOverrideUp":
                    TDPSlider.Maximum = (double)value;
                    break;
            }
        });
    }

    private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var screenFrequency = SystemManager.GetDesktopScreen().GetFrequency();

            FramerateQuarter.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Quarter));
            FramerateThird.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Third));
            FramerateHalf.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Half));
            FramerateFull.Text = Convert.ToString(screenFrequency.GetValue(Frequency.Full));
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

        var openFileDialog = new OpenFileDialog();
        if (openFileDialog.ShowDialog() == true)
            try
            {
                var path = openFileDialog.FileName;
                var folder = Path.GetDirectoryName(path);

                var file = openFileDialog.SafeFileName;
                var ext = Path.GetExtension(file);

                switch (ext)
                {
                    default:
                    case ".exe":
                        break;
                    case ".xml":
                        try
                        {
                            var doc = new XmlDocument();
                            doc.Load(path);

                            var Applications = doc.GetElementsByTagName("Applications");
                            foreach (XmlNode node in Applications)
                            foreach (XmlNode child in node.ChildNodes)
                                if (child.Name.Equals("Application"))
                                    if (child.Attributes is not null)
                                        foreach (XmlAttribute attribute in child.Attributes)
                                            switch (attribute.Name)
                                            {
                                                case "Executable":
                                                    path = Path.Combine(folder, attribute.InnerText);
                                                    file = Path.GetFileName(path);
                                                    break;
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

        switch ((MotionInput)cB_Input.SelectedIndex)
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
            // set lock
            profileLock = true;

            // disable button if is default profile or application is running
            b_DeleteProfile.IsEnabled =
                !currentProfile.ErrorCode.HasFlag(ProfileErrorCode.Default & ProfileErrorCode.Running);

            // prevent user from renaming default profile
            tB_ProfileName.IsEnabled = !currentProfile.Default;
            // prevent user from disabling default profile
            Toggle_EnableProfile.IsEnabled = !currentProfile.Default;
            // prevent user from disabling default profile layout
            Toggle_ControllerLayout.IsEnabled = !currentProfile.Default;

            // Profile info
            tB_ProfileName.Text = currentProfile.Name;
            tB_ProfilePath.Text = currentProfile.Path;
            Toggle_EnableProfile.IsOn = currentProfile.Enabled;

            // Global settings
            cB_Whitelist.IsChecked = currentProfile.Whitelisted;
            cB_Wrapper.IsChecked = currentProfile.XInputPlus;

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

            // GPU Clock control
            GPUToggle.IsOn = currentProfile.GPUOverrideEnabled;
            GPUSlider.Value = currentProfile.GPUOverrideValue != 0 ? currentProfile.GPUOverrideValue : 255 * 50;

            // Framerate limit
            FramerateToggle.IsOn = currentProfile.FramerateEnabled;
            FramerateSlider.Value = currentProfile.FramerateValue;

            // Layout settings
            Toggle_ControllerLayout.IsOn = currentProfile.LayoutEnabled;

            // UMC settings
            Toggle_UniversalMotion.IsOn = currentProfile.MotionEnabled;
            cB_Input.SelectedIndex = (int)currentProfile.MotionInput;
            cB_Output.SelectedIndex = (int)currentProfile.MotionOutput;
            tb_ProfileUMCAntiDeadzone.Value = currentProfile.MotionAntiDeadzone;
            cB_UMC_MotionDefaultOffOn.SelectedIndex = (int)currentProfile.MotionMode;

            // todo: improve me ?
            ProfilesPageHotkey.inputsChord.State = currentProfile.MotionTrigger.Clone() as ButtonState;
            ProfilesPageHotkey.DrawInput();

            // display warnings
            switch (currentProfile.ErrorCode)
            {
                default:
                case ProfileErrorCode.None:
                    WarningBorder.Visibility = Visibility.Collapsed;
                    cB_Whitelist.IsEnabled = true;
                    cB_Wrapper.IsEnabled = true;
                    break;

                case ProfileErrorCode.Running:
                case ProfileErrorCode.MissingExecutable:
                case ProfileErrorCode.MissingPath:
                case ProfileErrorCode.MissingPermission:
                case ProfileErrorCode.Default:
                    WarningBorder.Visibility = Visibility.Visible;
                    WarningContent.Text = EnumUtils.GetDescriptionFromEnumValue(currentProfile.ErrorCode);
                    cB_Whitelist.IsEnabled = false; // you can't whitelist an application without path
                    cB_Wrapper.IsEnabled = false; // you can't deploy wrapper on an application without path
                    break;
            }

            // release lock
            profileLock = false;
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
        // todo : move me to WPF
        UniversalSettings.IsEnabled = (bool)!cB_Whitelist.IsChecked;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.Whitelisted = (bool)cB_Whitelist.IsChecked;
        RequestUpdate();
    }

    private void cB_Wrapper_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.XInputPlus = (bool)cB_Wrapper.IsChecked;
        RequestUpdate();
    }

    private void Toggle_UniversalMotion_Toggled(object sender, RoutedEventArgs e)
    {
        if (currentProfile is null)
            return;

        // todo : move me to WPF
        cB_Whitelist.IsEnabled = !Toggle_UniversalMotion.IsOn && !currentProfile.Default;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionEnabled = Toggle_UniversalMotion.IsOn;
        RequestUpdate();
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void Toggle_EnableProfile_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.Enabled = Toggle_EnableProfile.IsOn;
        RequestUpdate();
    }

    private void cB_Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Input.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        // Check which input type is selected and automatically
        // set the most used output joystick accordingly.
        MotionInput input = (MotionInput)cB_Input.SelectedIndex;
        switch (input)
        {
            case MotionInput.PlayerSpace:
            case MotionInput.JoystickCamera:
            case MotionInput.AutoRollYawSwap:
                cB_Output.SelectedIndex = (int)MotionOutput.RightStick;
                break;
            case MotionInput.JoystickSteering:
                cB_Output.SelectedIndex = (int)MotionOutput.LeftStick;
                break;
        }

        Text_InputHint.Text = Profile.InputDescription[input];

        currentProfile.MotionInput = (MotionInput)cB_Input.SelectedIndex;
        RequestUpdate();
    }

    private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // TDP and AutoTDP are mutually exclusive
        var toggled = TDPToggle.IsOn;
        if (toggled)
            AutoTDPToggle.IsOn = false;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.TDPOverrideEnabled = TDPToggle.IsOn;
        RequestUpdate();
    }

    private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!TDPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
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
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.FramerateEnabled = FramerateToggle.IsOn;
        RequestUpdate();
    }

    private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!FramerateSlider.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var value = (int)FramerateSlider.Value;

            foreach (TextBlock tb in FramerateModeGrid.Children)
                tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            var TextBlock = (TextBlock)FramerateModeGrid.Children[value];
            TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
        });

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
        if (profileLock)
            return;

        currentProfile.AutoTDPEnabled = AutoTDPToggle.IsOn;
        RequestUpdate();
    }

    private void AutoTDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!AutoTDPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.AutoTDPRequestedFPS = (int)AutoTDPSlider.Value;
        RequestUpdate();
    }

    private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
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
        if (profileLock)
            return;

        currentProfile.GPUOverrideValue = (int)GPUSlider.Value;
        RequestUpdate();
    }

    private void TriggerCreated(Hotkey hotkey)
    {
        switch (hotkey.inputsHotkey.Listener)
        {
            case "shortcutProfilesPage@":
            {
                var hotkeyBorder = hotkey.GetControl();
                if (hotkeyBorder is null || hotkeyBorder.Parent is not null)
                    return;

                // pull hotkey
                ProfilesPageHotkey = hotkey;

                UMC_Activator.Children.Add(hotkeyBorder);
            }
                break;
        }
    }

    private void TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
    {
        switch (listener)
        {
            case "shortcutProfilesPage@":
            case "shortcutProfilesPage@@":
                currentProfile.MotionTrigger = inputs.State.Clone() as ButtonState;
                break;
        }
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
            Product = currentProfile.Name
        };
        layoutTemplate.Updated += Template_Updated;

        // set lock
        layoutLock = true;

        MainWindow.layoutPage.UpdateLayout(layoutTemplate);
        MainWindow.NavView_Navigate(MainWindow.layoutPage);

        // release lock
        layoutLock = false;
    }

    private void Template_Updated(LayoutTemplate layoutTemplate)
    {
        // wait until lock is released
        if (layoutLock)
            return;

        currentProfile.Layout = layoutTemplate.Layout;
        currentProfile.LayoutTitle = layoutTemplate.Name;
        RequestUpdate();
    }

    private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.EPPOverrideEnabled = EPPToggle.IsOn;
        RequestUpdate();
    }

    private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!EPPSlider.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
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
        if (profileLock)
            return;

        currentProfile.Name = tB_ProfileName.Text;
        RequestUpdate();
    }

    private void tb_ProfileGyroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileGyroValue.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.GyrometerMultiplier = (float)tb_ProfileGyroValue.Value;
        RequestUpdate();
    }

    private void tb_ProfileAcceleroValue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileAcceleroValue.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.AccelerometerMultiplier = (float)tb_ProfileAcceleroValue.Value;
        RequestUpdate();
    }

    private void cB_GyroSteering_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_GyroSteering.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.SteeringAxis = cB_GyroSteering.SelectedIndex;
        RequestUpdate();
    }

    private void cB_InvertHorizontal_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionInvertHorizontal = (bool)cB_InvertHorizontal.IsChecked;
        RequestUpdate();
    }

    private void cB_InvertVertical_Checked(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionInvertVertical = (bool)cB_InvertVertical.IsChecked;
        RequestUpdate();
    }

    private void cB_Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_Output.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionOutput = (MotionOutput)cB_Output.SelectedIndex;
        RequestUpdate();
    }

    private void tb_ProfileUMCAntiDeadzone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!tb_ProfileUMCAntiDeadzone.IsInitialized)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionAntiDeadzone = (float)tb_ProfileUMCAntiDeadzone.Value;
        RequestUpdate();
    }

    private void cB_UMC_MotionDefaultOffOn_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cB_UMC_MotionDefaultOffOn.SelectedIndex == -1)
            return;

        // wait until lock is released
        if (profileLock)
            return;

        currentProfile.MotionMode = (MotionMode)cB_UMC_MotionDefaultOffOn.SelectedIndex;
        RequestUpdate();
    }

    private void Toggle_ControllerLayout_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (profileLock)
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
        UpdateTimer.Stop();
        UpdateTimer.Start();
    }

    public void SubmitProfile(ProfileUpdateSource source = ProfileUpdateSource.ProfilesPage)
    {
        if (currentProfile is null)
            return;

        ProfileManager.UpdateOrCreateProfile(currentProfile, source);
    }
}