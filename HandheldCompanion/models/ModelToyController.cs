using SharpDX.XInput;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelToyController : Model
    {
        // Specific groups (move me)
        Model3DGroup JoystickLeftCover;
        Model3DGroup JoystickRightCover;
        Model3DGroup Smile1;
        Model3DGroup Smile2;
        Model3DGroup Smile3;
        Model3DGroup Smile4;
        Model3DGroup Smile5;
        Model3DGroup Smile6;
        Model3DGroup MainBodyBack;

        public ModelToyController() : base("ToyController")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];

            Color ColorPlasticBlue = (Color)ColorConverter.ConvertFromString("#7CC047");
            Color ColorPlasticGreen = (Color)ColorConverter.ConvertFromString("#7CC047");
            Color ColorPlasticRed = (Color)ColorConverter.ConvertFromString("#E8072F");
            Color ColorPlasticOrange = (Color)ColorConverter.ConvertFromString("#7CC047");
            Color ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#F8D902");
            Color ColorPlasticPurple = (Color)ColorConverter.ConvertFromString("#843CA8");
            Color ColorPlasticGreenFluorescent = (Color)ColorConverter.ConvertFromString("#BAD000");

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            DiffuseMaterial MaterialPlasticBlue = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlue));
            DiffuseMaterial MaterialPlasticGreen = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreen));
            DiffuseMaterial MaterialPlasticRed = new DiffuseMaterial(new SolidColorBrush(ColorPlasticRed));
            DiffuseMaterial MaterialPlasticOrange = new DiffuseMaterial(new SolidColorBrush(ColorPlasticOrange));
            DiffuseMaterial MaterialPlasticYellow = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellow));
            DiffuseMaterial MaterialPlasticPurple = new DiffuseMaterial(new SolidColorBrush(ColorPlasticPurple));
            DiffuseMaterial MaterialPlasticGreenFluorescent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreenFluorescent));

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-58.037f, 31.726f, 25.516f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(0.0f, 0.0f, 0.0f);
            JoystickMaxAngleDeg = 8.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-44.668f, 3.087f, 39.705);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(44.668f, 3.087f, 39.705);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-36.226f, -14.26f, 47.332f);
            UpwardVisibilityRotationPointRight = new Vector3D(36.226f, -14.26f, 47.332f);

            // load model(s)
            JoystickLeftCover = modelImporter.Load($"models/{ModelName}/Joystick-Left-Cover.obj");
            JoystickRightCover = modelImporter.Load($"models/{ModelName}/Joystick-Right-Cover.obj");
            Smile1 = modelImporter.Load($"models/{ModelName}/Smile1.obj");
            Smile2 = modelImporter.Load($"models/{ModelName}/Smile2.obj");
            Smile3 = modelImporter.Load($"models/{ModelName}/Smile3.obj");
            Smile4 = modelImporter.Load($"models/{ModelName}/Smile4.obj");
            Smile5 = modelImporter.Load($"models/{ModelName}/Smile5.obj");
            Smile6 = modelImporter.Load($"models/{ModelName}/Smile6.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");

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
            model3DGroup.Children.Add(JoystickLeftCover);
            model3DGroup.Children.Add(JoystickRightCover);
            model3DGroup.Children.Add(Smile1);
            model3DGroup.Children.Add(Smile2);
            model3DGroup.Children.Add(Smile3);
            model3DGroup.Children.Add(Smile4);
            model3DGroup.Children.Add(Smile5);
            model3DGroup.Children.Add(Smile6);
            model3DGroup.Children.Add(MainBodyBack);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }

            // specific button material(s)
            foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                int i = 0;
                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case GamepadButtonFlags.X:
                                DefaultMaterials[model3D] = MaterialPlasticBlue;
                                break;
                            case GamepadButtonFlags.Y:
                                DefaultMaterials[model3D] = MaterialPlasticYellow;
                                break;
                            case GamepadButtonFlags.A:
                                DefaultMaterials[model3D] = MaterialPlasticGreen;
                                break;
                            case GamepadButtonFlags.B:
                                DefaultMaterials[model3D] = MaterialPlasticRed;
                                break;
                        }
                        i++;
                    }
            }

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // specific material(s)
                if (model3D == MainBody || model3D == Smile1 || model3D == Smile1)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                if (model3D == MainBodyBack || model3D == LeftMotor || model3D == RightMotor)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreenFluorescent;
                    DefaultMaterials[model3D] = MaterialPlasticGreenFluorescent;
                }

                if (model3D == Smile3)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticRed;
                    DefaultMaterials[model3D] = MaterialPlasticRed;
                }

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;
            }
        }
    }
}
