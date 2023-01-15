using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelXBOX360 : IModel
    {
        // Specific groups (move me)
        Model3DGroup MainBodyCharger;
        Model3DGroup SpecialRing;
        Model3DGroup SpecialLED;
        Model3DGroup LeftShoulderBottom;
        Model3DGroup RightShoulderBottom;

        Model3DGroup B1Button;
        Model3DGroup B2Button;
        Model3DGroup B3Button;
        Model3DGroup B4Button;

        public ModelXBOX360() : base("XBOX360")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#707477");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#D4D4D4");
            var ColorPlasticSilver = (Color)ColorConverter.ConvertFromString("#CEDAE1");

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
            var MaterialPlasticSilver = new DiffuseMaterial(new SolidColorBrush(ColorPlasticSilver));

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
            SpecialRing = modelImporter.Load($"models/{ModelName}/SpecialRing.obj");
            SpecialLED = modelImporter.Load($"models/{ModelName}/SpecialLED.obj");
            LeftShoulderBottom = modelImporter.Load($"models/{ModelName}/LeftShoulderBottom.obj");
            RightShoulderBottom = modelImporter.Load($"models/{ModelName}/RightShoulderBottom.obj");

            B1Button = modelImporter.Load($"models/{ModelName}/B1Button.obj");
            B2Button = modelImporter.Load($"models/{ModelName}/B2Button.obj");
            B3Button = modelImporter.Load($"models/{ModelName}/B3Button.obj");
            B4Button = modelImporter.Load($"models/{ModelName}/B4Button.obj");

            // pull model(s)
            model3DGroup.Children.Add(MainBodyCharger);
            model3DGroup.Children.Add(SpecialRing);
            model3DGroup.Children.Add(SpecialLED);
            model3DGroup.Children.Add(LeftShoulderBottom);
            model3DGroup.Children.Add(RightShoulderBottom);

            model3DGroup.Children.Add(B1Button);
            model3DGroup.Children.Add(B2Button);
            model3DGroup.Children.Add(B3Button);
            model3DGroup.Children.Add(B4Button);

            // specific button material(s)
            foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ButtonFlags.B1:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case ButtonFlags.B2:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case ButtonFlags.B3:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case ButtonFlags.B4:
                                buttonMaterial = MaterialPlasticYellow;
                                break;
                            case ButtonFlags.Special:
                                buttonMaterial = MaterialPlasticSilver;
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
                if (model3D.Equals(MainBody) || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor) || model3D.Equals(LeftShoulderBottom) || model3D.Equals(RightShoulderBottom))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // specific face button material
                if (model3D.Equals(B1Button) || model3D.Equals(SpecialLED))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreenTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticGreenTransparent;
                    continue;
                }

                if (model3D.Equals(B2Button))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticRedTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticRedTransparent;
                    continue;
                }

                if (model3D.Equals(B3Button))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlueTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlueTransparent;
                    continue;
                }

                if (model3D.Equals(B4Button))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticYellowTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticYellowTransparent;
                    continue;
                }
            }

            base.DrawAccentHighligths();
        }
    }
}
