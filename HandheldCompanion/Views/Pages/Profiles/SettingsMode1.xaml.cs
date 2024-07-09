using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using LiveCharts;
using LiveCharts.Defaults;
using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Profiles;

/// <summary>
///     Interaction logic for SettingsMode1.xaml
/// </summary>
public partial class SettingsMode1 : Page
{
    private CrossThreadLock updateLock = new();

    private readonly int SteeringArraySize = 30;
    private readonly ChartValues<ObservablePoint> SteeringLinearityPoints;

    public SettingsMode1()
    {
        InitializeComponent();
    }

    public SettingsMode1(string Tag) : this()
    {
        this.Tag = Tag;

        lvCartesianChart.DataTooltip = null;

        MotionManager.SettingsMode1Update += MotionManager_SettingsMode1Update;

        SteeringLinearityPoints = [];
        for (var i = 0; i < SteeringArraySize; i++)
        {
            var value = i / (double)(SteeringArraySize - 1);
            SteeringLinearityPoints.Add(new ObservablePoint { X = value, Y = value });
        }

        lvLineSeriesDefault.Values = new ChartValues<double> { 0, 1 };
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
                    SliderDeadzoneAngle.Value = ProfilesPage.selectedProfile.SteeringDeadzone;
                    SliderPower.Value = ProfilesPage.selectedProfile.SteeringPower;
                    SliderSteeringAngle.Value = ProfilesPage.selectedProfile.SteeringMaxAngle;

                    lvLineSeriesValues.Values = GeneratePoints(ProfilesPage.selectedProfile.SteeringPower);
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

    private void MotionManager_SettingsMode1Update(Vector2 deviceAngle)
    {
        Rotate_Needle(-deviceAngle.Y);
    }

    private void Rotate_Needle(float y)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() => { lvAngularGauge.Value = y; });
    }

    private void SliderSteeringAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.SteeringMaxAngle = (float)SliderSteeringAngle.Value;
        ProfilesPage.UpdateProfile();
    }

    private void SliderPower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        lvLineSeriesValues.Values = GeneratePoints(SliderPower.Value);

        ProfilesPage.selectedProfile.SteeringPower = (float)SliderPower.Value;
        ProfilesPage.UpdateProfile();
    }

    private void SliderDeadzoneAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.selectedProfile is null)
            return;

        // prevent update loop
        if (updateLock.IsEntered())
            return;

        ProfilesPage.selectedProfile.SteeringDeadzone = (float)SliderDeadzoneAngle.Value;
        ProfilesPage.UpdateProfile();
    }

    private ChartValues<ObservablePoint> GeneratePoints(double Power)
    {
        for (var i = 0; i < SteeringArraySize; i++)
            SteeringLinearityPoints[i].Y = (float)Math.Pow(SteeringLinearityPoints[i].X, Power);

        return SteeringLinearityPoints;
    }
}