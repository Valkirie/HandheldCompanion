using HandheldCompanion.Actions;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HandheldCompanion.Views.Pages.Profiles;

/// <summary>
///     Interaction logic for SettingsMode0.xaml
/// </summary>
public partial class SettingsMode0 : Page
{
    private CrossThreadLock updateLock = new();

    private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_AIMING;
    private Hotkey GyroHotkey = new(gyroButtonFlags) { IsInternal = true };

    public SettingsMode0()
    {
        DataContext = new SettingsMode0ViewModel();
        InitializeComponent();
    }

    public SettingsMode0(string Tag) : this()
    {
        this.Tag = Tag;

        MotionManager.SettingsMode0Update += MotionManager_SettingsMode0Update;
        HotkeysManager.Updated += HotkeysManager_Updated;

        // store hotkey to manager
        HotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
    }

    public void SetProfile()
    {
        if (updateLock.TryEnter())
        {
            try
            {
                // UI thread (async)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SliderSensitivityX.Value = ProfilesPage.selectedProfile.MotionSensivityX;
                    SliderSensitivityY.Value = ProfilesPage.selectedProfile.MotionSensivityY;
                    tb_ProfileAimingDownSightsMultiplier.Value = ProfilesPage.selectedProfile.AimingSightsMultiplier;
                    Toggle_FlickStick.IsOn = ProfilesPage.selectedProfile.FlickstickEnabled;
                    tb_ProfileFlickDuration.Value = ProfilesPage.selectedProfile.FlickstickDuration * 1000;
                    tb_ProfileStickSensitivity.Value = ProfilesPage.selectedProfile.FlickstickSensivity;

                    GyroHotkey.inputsChord.ButtonState = ProfilesPage.selectedProfile.AimingSightsTrigger.Clone() as ButtonState;
                    HotkeysManager.UpdateOrCreateHotkey(GyroHotkey);

                    // temp
                    StackCurve.Children.Clear();
                    foreach (KeyValuePair<double, double> elem in ProfilesPage.selectedProfile.MotionSensivityArray)
                    {
                        // skip first item ?
                        if (elem.Key == 0)
                            continue;

                        double height = elem.Value * StackCurve.Height;
                        Thumb thumb = new Thumb
                        {
                            Tag = elem.Key,
                            Width = 8,
                            MaxHeight = StackCurve.Height,
                            Height = height,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            BorderThickness = new Thickness(0),
                            IsEnabled = false // prevent the control from being clickable
                        };

                        // Bind the Stroke property to a dynamic resource
                        thumb.SetResourceReference(Thumb.BackgroundProperty, "SystemControlHighlightAltListAccentLowBrush");
                        thumb.SetResourceReference(Thumb.BorderBrushProperty, "SystemControlHighlightAltListAccentHighBrush");

                        StackCurve.Children.Add(thumb);
                    }
                });
            }
            finally
            {
                updateLock.Exit();
            }
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
    }

    private void MotionManager_SettingsMode0Update(Vector3 gyrometer)
    {
        Highlight_Thumb(Math.Max(Math.Max(Math.Abs(gyrometer.Z), Math.Abs(gyrometer.X)), Math.Abs(gyrometer.Y)));
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        ProfilesPage.UpdateProfile();
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        ProfilesPage.UpdateProfile();
    }

    private void Highlight_Thumb(float value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            double dist_x = value / IDevice.GetCurrent().GamepadMotion.GetCalibration().GetGyroThreshold();

            foreach (Control control in StackCurve.Children)
            {
                double x = (double)control.Tag;

                if (dist_x > x)
                    control.BorderThickness = new Thickness(0, 0, 0, 20);
                else
                    control.BorderThickness = new Thickness(0);
            }
        });
    }

    private void StackCurve_MouseDown(object sender, MouseButtonEventArgs e)
    {
        StackCurve_MouseMove(sender, e);
    }

    private void StackCurve_MouseMove(object sender, MouseEventArgs e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        Control thumb = null;

        foreach (Control control in StackCurve.Children)
        {
            var position = e.GetPosition(control);
            var dist_x = Math.Abs(position.X);

            // Bind the Stroke property to a dynamic resource
            control.SetResourceReference(BackgroundProperty, "SystemControlHighlightAltListAccentLowBrush");

            if (dist_x <= control.Width)
                thumb = control;
        }

        if (thumb is null)
            return;

        // Bind the Stroke property to a dynamic resource
        thumb.SetResourceReference(Thumb.BackgroundProperty, "SystemControlHighlightAltListAccentHighBrush");

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var x = (double)thumb.Tag;
            thumb.Height = StackCurve.ActualHeight - e.GetPosition(StackCurve).Y;
            ProfilesPage.selectedProfile.MotionSensivityArray[x] = thumb.Height / StackCurve.Height;
            ProfilesPage.UpdateProfile();
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // default preset
        foreach (Control Thumb in StackCurve.Children)
        {
            var x = (double)Thumb.Tag;
            Thumb.Height = StackCurve.Height / 2.0f;
            ProfilesPage.selectedProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            ProfilesPage.UpdateProfile();
        }
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        // agressive preset
        var tempx = 24f / Profile.SensivityArraySize;
        foreach (Control Thumb in StackCurve.Children)
        {
            var x = (double)Thumb.Tag;
            var value = (float)(-Math.Sqrt(x * tempx) + 0.85f);

            Thumb.Height = StackCurve.Height * value;
            ProfilesPage.selectedProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            ProfilesPage.UpdateProfile();
        }
    }

    private void Button_Click_2(object sender, RoutedEventArgs e)
    {
        // precise preset
        var tempx = 12f / Profile.SensivityArraySize;
        foreach (Control Thumb in StackCurve.Children)
        {
            var x = (double)Thumb.Tag;
            var value = (float)(Math.Sqrt(x * tempx) + 0.25f - tempx * x);

            Thumb.Height = StackCurve.Height * value;
            ProfilesPage.selectedProfile.MotionSensivityArray[x] = Thumb.Height / StackCurve.Height;
            ProfilesPage.UpdateProfile();
        }
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void SliderAimingDownSightsMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.AimingSightsMultiplier = (float)tb_ProfileAimingDownSightsMultiplier.Value;
        ProfilesPage.UpdateProfile();
    }

    private void Toggle_FlickStick_Toggled(object sender, RoutedEventArgs e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.FlickstickEnabled = Toggle_FlickStick.IsOn;
        ProfilesPage.UpdateProfile();
    }

    private void SliderFlickDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.FlickstickDuration = (float)tb_ProfileFlickDuration.Value / 1000;
        ProfilesPage.UpdateProfile();
    }

    private void SliderStickSensivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.FlickstickSensivity = (float)tb_ProfileStickSensitivity.Value;
        ProfilesPage.UpdateProfile();
    }

    private void HotkeysManager_Updated(Hotkey hotkey)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        if (!ProfilesPage.selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions currentAction))
            return;

        if (hotkey.ButtonFlags != gyroButtonFlags)
            return;

        // update gyro hotkey
        GyroHotkey = hotkey;

        ProfilesPage.selectedProfile.AimingSightsTrigger = hotkey.inputsChord.ButtonState.Clone() as ButtonState;
        ProfilesPage.UpdateProfile();
    }
}