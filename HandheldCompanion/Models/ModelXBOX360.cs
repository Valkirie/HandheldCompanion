using SharpDX.XInput;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelXBOX360 : Model
    {
        // Specific groups (move me)
        Model3DGroup MainBodyCharger;
        Model3DGroup XBoxButton;
        Model3DGroup XboxButtonRing;

        public ModelXBOX360() : base("XBOX360")
        {
            // colors
            ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#707477");
            ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#D4D4D4");
            ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-42.231f, -6.10f, 21.436f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(21.013f, -6.1f, -3.559f);
            JoystickMaxAngleDeg = 19.0f;
            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-44.668f, 3.087f, 39.705);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(44.668f, 3.087f, 39.705);
            TriggerMaxAngleDeg = 16.0f;

            // load model(s)
            MainBodyCharger = modelImporter.Load($"models/{ModelName}/MainBody-Charger.obj");
            XBoxButton = modelImporter.Load($"models/{ModelName}/XBoxButton.obj");
            XboxButtonRing = modelImporter.Load($"models/{ModelName}/XboxButtonRing.obj");

            // map model(s)
            foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                switch (button)
                {
                    case GamepadButtonFlags.A:
                    case GamepadButtonFlags.B:
                    case GamepadButtonFlags.X:
                    case GamepadButtonFlags.Y:

                        string filename = $"models/{ModelName}/{button}-Letter.obj";
                        if (File.Exists(filename))
                        {
                            Model3DGroup model = modelImporter.Load(filename);
                            ButtonMap[button].Add(model);

                            // pull model
                            model3DGroup.Children.Add(model);
                        }

                        break;
                }
            }

            // pull model(s)
            model3DGroup.Children.Add(MainBodyCharger);
            model3DGroup.Children.Add(XBoxButton);
            model3DGroup.Children.Add(XboxButtonRing);

            foreach (Model3DGroup model3D in model3DGroup.Children)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;

            // specific color(s)
            ((GeometryModel3D)MainBody.Children[0]).Material = MaterialPlasticWhite;
            ((GeometryModel3D)LeftMotor.Children[0]).Material = MaterialPlasticWhite;
            ((GeometryModel3D)RightMotor.Children[0]).Material = MaterialPlasticWhite;
        }
    }
}
