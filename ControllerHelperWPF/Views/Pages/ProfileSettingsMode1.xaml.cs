using ControllerCommon;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using System;
using System.Diagnostics;

namespace ControllerHelperWPF.Views.Pages
{
    /// <summary>
    /// Interaction logic for ProfileSettingsMode1.xaml
    /// </summary>
    public partial class ProfileSettingsMode1 : Page
    {
        private Profile profileCurrent;
        private PipeClient pipeClient;

        private int SteeringArraySize = 80;
        private ChartValues<ObservablePoint> SteeringLinearityPoints;

        public ProfileSettingsMode1()
        {
            InitializeComponent();

            SteeringLinearityPoints = new();
            for (int i = 0; i < SteeringArraySize; i++)
            {
                double value = (double)i / (double)(SteeringArraySize - 1);
                SteeringLinearityPoints.Add(new ObservablePoint() { X = value, Y = value });
            }

            lvLineSeriesDefault.Values = new ChartValues<double>() { 0, 1 };
        }

        public ProfileSettingsMode1(string Tag, Profile profileCurrent, PipeClient pipeClient) : this()
        {
            this.Tag = Tag;

            this.profileCurrent = profileCurrent;
            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
            this.pipeClient.SendMessage(new PipeNavigation((string)this.Tag));

            SliderDeadzoneAngle.Value = profileCurrent.steering_deadzone;
            SliderDeadzoneCompensation.Value = profileCurrent.steering_deadzone_compensation;
            SliderPower.Value = profileCurrent.steering_power;
            SliderSteeringAngle.Value = profileCurrent.steering_max_angle;

            lvLineSeriesValues.Values = GeneratePoints(profileCurrent.steering_power);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SENSOR:
                    PipeSensor sensor = (PipeSensor)message;

                    switch(sensor.type)
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
            this.Dispatcher.Invoke(() =>
            {
                lvAngularGauge.Value = y;
            });
        }

        private void SliderSteeringAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_max_angle = (float)SliderSteeringAngle.Value;
        }

        private void SliderPower_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_power = (float)SliderPower.Value;
            lvLineSeriesValues.Values = GeneratePoints(SliderPower.Value);
        }

        private void SliderDeadzoneAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_deadzone = (float)SliderDeadzoneAngle.Value;
        }

        private void SliderDeadzoneCompensation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (profileCurrent is null)
                return;

            profileCurrent.steering_deadzone_compensation = (float)SliderDeadzoneCompensation.Value;
        }

        private ChartValues<ObservablePoint> GeneratePoints(double Power)
        {
            for (int i = 0; i < SteeringArraySize; i++)
                SteeringLinearityPoints[i].Y = (float)Math.Pow(SteeringLinearityPoints[i].X, Power);

            return SteeringLinearityPoints;
        }
    }
}
