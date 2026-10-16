using HandheldCompanion.Actions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Profiles;

/// <summary>
///     Interaction logic for SettingsMode0.xaml
/// </summary>
public partial class SettingsMode0 : Page
{
    private CrossThreadLock updateLock = new();

    private const ButtonFlags gyroButtonFlags = ButtonFlags.HOTKEY_GYRO_AIMING;
    private Hotkey GyroHotkey = new(gyroButtonFlags) { IsInternal = true, Name = "HOTKEY_GYRO_AIMING" };
    private readonly SettingsMode0ViewModel _viewModel;

    public SettingsMode0()
    {
        _viewModel = new SettingsMode0ViewModel();
        DataContext = _viewModel;
        InitializeComponent();

        _viewModel.InitializeMotionChart(motionSensitivityChart, motionSensitivityLineSeries, motionSensitivityIndicator);
        _viewModel.MotionSensitivityCurveChanged += ViewModel_MotionSensitivityCurveChanged;
    }

    public SettingsMode0(string Tag) : this()
    {
        this.Tag = Tag;

        MotionManager.SettingsMode0Update += MotionManager_SettingsMode0Update;
        ManagerFactory.hotkeysManager.Updated += HotkeysManager_Updated;

        // store hotkey to manager
        ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey);
    }

    public void SetProfile()
    {
        if (updateLock.TryEnter())
        {
            try
            {
                // UI thread
                UIHelper.TryInvoke(() =>
                {
                    Profile? selectedProfile = ProfilesPage.selectedProfile;
                    if (selectedProfile is null)
                        return;

                    SliderSensitivityX.Value = selectedProfile.MotionSensivityX;
                    SliderSensitivityY.Value = selectedProfile.MotionSensivityY;
                    tb_ProfileAimingDownSightsMultiplier.Value = selectedProfile.AimingSightsMultiplier;

                    GyroHotkey.inputsChord.ButtonState = (ButtonState)selectedProfile.AimingSightsTrigger.Clone();
                    ManagerFactory.hotkeysManager.UpdateOrCreateHotkey(GyroHotkey);

                    _viewModel.SetMotionSensitivityCurve(selectedProfile.MotionSensitivityArray);
                });
            }
            catch { }
            finally
            {
                updateLock.Exit();
            }
        }
    }

    public void Dispose()
    {
        MotionManager.SettingsMode0Update -= MotionManager_SettingsMode0Update;
        ManagerFactory.hotkeysManager.Updated -= HotkeysManager_Updated;
        _viewModel.MotionSensitivityCurveChanged -= ViewModel_MotionSensitivityCurveChanged;

        _viewModel.Dispose();
    }

    private void MotionManager_SettingsMode0Update(Vector3 gyrometer)
    {
        UIHelper.TryInvoke(() => _viewModel.UpdateMotionSensitivityIndicator(Math.Max(Math.Max(Math.Abs(gyrometer.Z), Math.Abs(gyrometer.X)), Math.Abs(gyrometer.Y))));
    }

    private void SliderSensitivityX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.MotionSensivityX = (float)SliderSensitivityX.Value;
        ProfilesPage.SubmitProfile();
    }

    private void SliderSensitivityY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.MotionSensivityY = (float)SliderSensitivityY.Value;
        ProfilesPage.SubmitProfile();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyDefaultMotionSensitivityPreset();
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyAggressiveMotionSensitivityPreset();
    }

    private void Button_Click_2(object sender, RoutedEventArgs e)
    {
        _viewModel.ApplyPreciseMotionSensitivityPreset();
    }

    private void ViewModel_MotionSensitivityCurveChanged(double[] curve)
    {
        if (ProfilesPage.selectedProfile is not Profile selectedProfile)
            return;

        selectedProfile.MotionSensitivityArray = curve;
        ProfilesPage.SubmitProfile();
    }

    private void SliderAimingDownSightsMultiplier_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.AimingSightsMultiplier = (float)tb_ProfileAimingDownSightsMultiplier.Value;
        ProfilesPage.SubmitProfile();
    }

    private void HotkeysManager_Updated(Hotkey hotkey)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        if (!ProfilesPage.selectedProfile.Layout.GyroLayout.TryGetValue(AxisLayoutFlags.Gyroscope, out IActions? currentAction))
            return;

        if (hotkey.ButtonFlags != gyroButtonFlags)
            return;

        // update gyro hotkey
        GyroHotkey = hotkey;

        ProfilesPage.selectedProfile.AimingSightsTrigger = (ButtonState)hotkey.inputsChord.ButtonState.Clone();
        ProfilesPage.SubmitProfile();
    }
}
