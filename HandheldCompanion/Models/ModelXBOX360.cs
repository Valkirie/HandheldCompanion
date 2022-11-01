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
        Model3DGroup LeftShoulderBottom;
        Model3DGroup RightShoulderBottom;

        Model3DGroup AButton;
        Model3DGroup BButton;
        Model3DGroup XButton;
        Model3DGroup YButton;

        public ModelXBOX360() : base("XBOX360")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#707477");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#D4D4D4");

            var ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#faa51f");
            var ColorPlasticGreen = (Color)ColorConverter.ConvertFromString("#7cb63b");
            var ColorPlasticRed = (Color)ColorConverter.ConvertFromString("#ff5f4b");
            var ColorPlasticBlue = (Color)ColorConverter.ConvertFromString("#6ac4f6");

            var ColorPlasticYellowTransparent = ColorPlasticYellow;
            var ColorPlasticGreenTransparent = ColorPlasticGreen;
            var ColorPlasticRedTransparent = ColorPlasticRed;
            var ColorPlasticBlueTransparent = ColorPlasticBlue;

            byte TransparancyAmount = 150;
            ColorPlasticYellowTransparent.A = TransparancyAmount;
            ColorPlasticGreenTransparent.A = TransparancyAmount;
            ColorPlasticRedTransparent.A = TransparancyAmount;
            ColorPlasticBlueTransparent.A = TransparancyAmount;

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

            var MaterialPlasticYellow = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellow));
            var MaterialPlasticGreen = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreen));
            var MaterialPlasticRed = new DiffuseMaterial(new SolidColorBrush(ColorPlasticRed));
            var MaterialPlasticBlue = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlue));

            var MaterialPlasticYellowTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellowTransparent));
            var MaterialPlasticGreenTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreenTransparent));
            var MaterialPlasticRedTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticRedTransparent));
            var MaterialPlasticBlueTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlueTransparent));

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-42.231f, -6.10f, 21.436f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(21.013f, -6.1f, -3.559f);
            JoystickMaxAngleDeg = 19.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-44.668f, 3.087f, 39.705);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(44.668f, 3.087f, 39.705);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-36.226f, -14.26f, 47.332f);
            UpwardVisibilityRotationPointRight = new Vector3D(36.226f, -14.26f, 47.332f);

            // load model(s)
            MainBodyCharger = modelImporter.Load($"models/{ModelName}/MainBody-Charger.obj");
            XBoxButton = modelImporter.Load($"models/{ModelName}/XBoxButton.obj");
            XboxButtonRing = modelImporter.Load($"models/{ModelName}/XboxButtonRing.obj");
            LeftShoulderBottom = modelImporter.Load($"models/{ModelName}/LeftShoulderBottom.obj");
            RightShoulderBottom = modelImporter.Load($"models/{ModelName}/RightShoulderBottom.obj");

            AButton = modelImporter.Load($"models/{ModelName}/AButton.obj");
            BButton = modelImporter.Load($"models/{ModelName}/BButton.obj");
            XButton = modelImporter.Load($"models/{ModelName}/XButton.obj");
            YButton = modelImporter.Load($"models/{ModelName}/YButton.obj");

            // pull model(s)
            model3DGroup.Children.Add(MainBodyCharger);
            model3DGroup.Children.Add(XBoxButton);
            model3DGroup.Children.Add(XboxButtonRing);
            model3DGroup.Children.Add(LeftShoulderBottom);
            model3DGroup.Children.Add(RightShoulderBottom);

            model3DGroup.Children.Add(AButton);
            model3DGroup.Children.Add(BButton);
            model3DGroup.Children.Add(XButton);
            model3DGroup.Children.Add(YButton);

            // specific button material(s)
            foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case GamepadButtonFlags.A:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case GamepadButtonFlags.B:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case GamepadButtonFlags.X:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case GamepadButtonFlags.Y:
                                buttonMaterial = MaterialPlasticYellow;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticBlack;
                                break;
                        }

                        DefaultMaterials[model3D] = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).Material = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).BackMaterial = buttonMaterial;

                    }
            }

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;

                // specific material(s)
                if (model3D == MainBody || model3D == LeftMotor || model3D == RightMotor || model3D == LeftShoulderBottom || model3D == RightShoulderBottom)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // specific face button material
                if (model3D == AButton)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreenTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticGreenTransparent;
                    continue;
                }

                if (model3D == BButton)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticRedTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticRedTransparent;
                    continue;
                }

                if (model3D == XButton)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlueTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlueTransparent;
                    continue;
                }

                if (model3D == YButton)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticYellowTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticYellowTransparent;
                    continue;
                }
            }

            DrawHighligths();
        }
    }
}
