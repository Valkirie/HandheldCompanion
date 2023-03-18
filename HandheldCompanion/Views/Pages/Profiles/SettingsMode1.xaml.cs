using ControllerCommon.Pipes;
using LiveCharts;
using LiveCharts.Defaults;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.Pages.Profiles
{
    /// <summary>
    /// Interaction logic for SettingsMode1.xaml
    /// </summary>
    public partial class SettingsMode1 : Page
    {
        private int SteeringArraySize = 30;
        private ChartValues<ObservablePoint> SteeringLinearityPoints;

        public SettingsMode1()
        {
            InitializeComponent();
        }

        public SettingsMode1(string Tag) : this()
        {
            this.Tag = Tag;

            PipeClient.ServerMessage += OnServerMessage;

            SteeringLinearityPoints = new();
            for (int i = 0; i < SteeringArraySize; i++)
            {
                double value = (double)i / (double)(SteeringArraySize - 1);
                SteeringLinearityPoints.Add(new ObservablePoint() { X = value, Y = value });
            }

            lvLineSeriesDefault.Values = new ChartValues<double>() { 0, 1 };
        }

        public void SetProfile()
        {
            SliderDeadzoneAngle.Value = ProfilesPage.currentProfile.SteeringDeadzone;
            SliderPower.Value = ProfilesPage.currentProfile.SteeringPower;
            SliderSteeringAngle.Value = ProfilesPage.currentProfile.SteeringMaxAngle;

            lvLineSeriesValues.Values = GeneratePoints(ProfilesPage.currentProfile.SteeringPower);
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
                    PipeSensor sensor = (PipeSensor)message;

                    switch (sensor.type)
                    {
                        case SensorType.Inclinometer:
                            Rotate_Needle(-sensor.y);
                            break;
                    }
                    break;
            }
        }

        private void Rotate_Needle(float y)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                lvAngularGauge.Value = y;
            });
        }

        private void SliderSteeringAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.SteeringMaxAngle = (float)SliderSteeringAngle.Value;
        }

        private void SliderPower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.SteeringPower = (float)SliderPower.Value;
            lvLineSeriesValues.Values = GeneratePoints(SliderPower.Value);
        }

        private void SliderDeadzoneAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ProfilesPage.currentProfile is null)
                return;

            ProfilesPage.currentProfile.SteeringDeadzone = (float)SliderDeadzoneAngle.Value;
        }

        private ChartValues<ObservablePoint> GeneratePoints(double Power)
        {
            for (int i = 0; i < SteeringArraySize; i++)
                SteeringLinearityPoints[i].Y = (float)Math.Pow(SteeringLinearityPoints[i].X, Power);

            return SteeringLinearityPoints;
        }
    }
}
