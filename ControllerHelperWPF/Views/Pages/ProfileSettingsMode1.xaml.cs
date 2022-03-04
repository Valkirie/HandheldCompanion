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

        public ProfileSettingsMode1()
        {
            InitializeComponent();

            LinearDefault = new ChartValues<double> { 0, 1 };
            SteeringLinearityPoints = new ChartValues<ObservablePoint>();

            DataContext = this;
        }

        public ProfileSettingsMode1(Profile profileCurrent, PipeClient pipeClient) : this()
        {
            this.profileCurrent = profileCurrent;
            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;

            SliderDeadzoneAngle.Value = profileCurrent.steering_deadzone;
            SliderDeadzoneCompensation.Value = profileCurrent.steering_deadzone_compensation;
            SliderPower.Value = profileCurrent.steering_power;
            SliderSteeringAngle.Value = profileCurrent.steering_max_angle;

            GeneratePoints(profileCurrent.steering_power);
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
                    // TODO get correct(?) sensor value here from inclino and push through to GUI.
                    System.Diagnostics.Debug.WriteLine("My text");
                    InclinoY = sensor.y;
                    Debug.WriteLine("My text");
                    System.Diagnostics.Debug.WriteLine(InclinoY);
                    break;
            }
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
            GeneratePoints((float)SliderPower.Value);
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

        private void GeneratePoints(float Power)
        {
            int ArraySize = 80;

            if (SteeringLinearityPoints.Count == 0)
            {
                for (int i = 0; i < ArraySize; i++)
                {
                    double value = (double)i / (double)(ArraySize - 1);
                    SteeringLinearityPoints.Add(new ObservablePoint
                    {
                        X = value,
                        Y = value
                    });
                } 
            }

            if (SteeringLinearityPoints.Count == ArraySize)
            {
                for (int i = 0; i < ArraySize; i++)
                {
                    SteeringLinearityPoints[i].Y = (float)Math.Pow(SteeringLinearityPoints[i].X, Power);
                }
            }
        }

        public ChartValues<double> LinearDefault { get; set; }
        public ChartValues<ObservablePoint> SteeringLinearityPoints { get; set; }
        public float InclinoY { get; set; }
    }
}
