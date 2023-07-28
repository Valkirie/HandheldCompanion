using System;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Pipes;
using LiveCharts;
using LiveCharts.Defaults;

namespace HandheldCompanion.Views.Pages.Profiles;

/// <summary>
///     Interaction logic for SettingsMode1.xaml
/// </summary>
public partial class SettingsMode1 : Page
{
    private readonly int SteeringArraySize = 30;
    private readonly ChartValues<ObservablePoint> SteeringLinearityPoints;
    private bool profileLock;

    public SettingsMode1()
    {
        InitializeComponent();
    }

    public SettingsMode1(string Tag) : this()
    {
        this.Tag = Tag;

        lvCartesianChart.DataTooltip = null;

        PipeClient.ServerMessage += OnServerMessage;

        SteeringLinearityPoints = new ChartValues<ObservablePoint>();
        for (var i = 0; i < SteeringArraySize; i++)
        {
            var value = i / (double)(SteeringArraySize - 1);
            SteeringLinearityPoints.Add(new ObservablePoint { X = value, Y = value });
        }

        lvLineSeriesDefault.Values = new ChartValues<double> { 0, 1 };
    }

    public void SetProfile()
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // set lock
            profileLock = true;

            SliderDeadzoneAngle.Value = ProfilesPage.currentProfile.SteeringDeadzone;
            SliderPower.Value = ProfilesPage.currentProfile.SteeringPower;
            SliderSteeringAngle.Value = ProfilesPage.currentProfile.SteeringMaxAngle;

            lvLineSeriesValues.Values = GeneratePoints(ProfilesPage.currentProfile.SteeringPower);

            // release lock
            profileLock = false;
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
        PipeClient.ServerMessage -= OnServerMessage;
    }

    private void OnServerMessage(PipeMessage message)
    {
        switch (message.code)
        {
            case PipeCode.SERVER_SENSOR:
                var sensor = (PipeSensor)message;

                switch (sensor.sensorType)
                {
                    case SensorType.Inclinometer:
                        Rotate_Needle(-sensor.reading.Y);
                        break;
                }

                break;
        }
    }

    private void Rotate_Needle(float y)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { lvAngularGauge.Value = y; });
    }

    private void SliderSteeringAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.currentProfile is null)
            return;

        ProfilesPage.currentProfile.SteeringMaxAngle = (float)SliderSteeringAngle.Value;
        ProfilesPage.RequestUpdate();
    }

    private void SliderPower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.currentProfile is null)
            return;

        lvLineSeriesValues.Values = GeneratePoints(SliderPower.Value);

        ProfilesPage.currentProfile.SteeringPower = (float)SliderPower.Value;
        ProfilesPage.RequestUpdate();
    }

    private void SliderDeadzoneAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ProfilesPage.currentProfile is null)
            return;

        ProfilesPage.currentProfile.SteeringDeadzone = (float)SliderDeadzoneAngle.Value;
        ProfilesPage.RequestUpdate();
    }

    private ChartValues<ObservablePoint> GeneratePoints(double Power)
    {
        for (var i = 0; i < SteeringArraySize; i++)
            SteeringLinearityPoints[i].Y = (float)Math.Pow(SteeringLinearityPoints[i].X, Power);

        return SteeringLinearityPoints;
    }
}