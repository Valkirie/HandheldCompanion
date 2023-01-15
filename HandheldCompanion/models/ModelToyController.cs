using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelToyController : IModel
    {
        // Specific groups (move me)
        Model3DGroup JoystickLeftCover;
        Model3DGroup JoystickRightCover;
        Model3DGroup MainBodyBack;

        Model3DGroup Smile1;
        Model3DGroup Smile2;
        Model3DGroup Smile3;
        Model3DGroup Smile4;
        Model3DGroup Smile5;
        Model3DGroup Smile6;

        Model3DGroup B1Letter;
        Model3DGroup B1LetterInside;
        Model3DGroup B2Letter;
        Model3DGroup B3Letter;
        Model3DGroup B3LetterInside;
        Model3DGroup B4Letter;
        Model3DGroup B4LetterInside1;
        Model3DGroup B4LetterInside2;

        Model3DGroup DPadLeft1;
        Model3DGroup DPadUp2;
        Model3DGroup DPadRight3;
        Model3DGroup DPadDown4;

        public ModelToyController() : base("ToyController")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");
            var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];

            Color ColorPlasticBlue = (Color)ColorConverter.ConvertFromString("#02BCE3");
            Color ColorPlasticGreen = (Color)ColorConverter.ConvertFromString("#7BBF46");
            Color ColorPlasticRed = (Color)ColorConverter.ConvertFromString("#E8072F");
            Color ColorPlasticOrange = (Color)ColorConverter.ConvertFromString("#F38200");
            Color ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#F8DB01");
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

            UpwardVisibilityRotationAxisLeft = new Vector3D(0.914607, 0, 0.404344);
            UpwardVisibilityRotationAxisRight = new Vector3D(0.920505, 0, -0.390731);
            UpwardVisibilityRotationPointLeft = new Vector3D(-67.174f, -6.3f, 75.475f);
            UpwardVisibilityRotationPointRight = new Vector3D(62.3f, -6.3f, 62.526f);

            // load model(s)
            JoystickLeftCover = modelImporter.Load($"models/{ModelName}/Joystick-Left-Cover.obj");
            JoystickRightCover = modelImporter.Load($"models/{ModelName}/Joystick-Right-Cover.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");

            Smile1 = modelImporter.Load($"models/{ModelName}/Smile1.obj");
            Smile2 = modelImporter.Load($"models/{ModelName}/Smile2.obj");
            Smile3 = modelImporter.Load($"models/{ModelName}/Smile3.obj");
            Smile4 = modelImporter.Load($"models/{ModelName}/Smile4.obj");
            Smile5 = modelImporter.Load($"models/{ModelName}/Smile5.obj");
            Smile6 = modelImporter.Load($"models/{ModelName}/Smile6.obj");

            B1Letter = modelImporter.Load($"models/{ModelName}/B1Letter.obj");
            B1LetterInside = modelImporter.Load($"models/{ModelName}/B1LetterInside.obj");
            B2Letter = modelImporter.Load($"models/{ModelName}/B2Letter.obj");
            B3Letter = modelImporter.Load($"models/{ModelName}/B3Letter.obj");
            B3LetterInside = modelImporter.Load($"models/{ModelName}/B3LetterInside.obj");
            B4Letter = modelImporter.Load($"models/{ModelName}/B4Letter.obj");
            B4LetterInside1 = modelImporter.Load($"models/{ModelName}/B4LetterInside1.obj");
            B4LetterInside2 = modelImporter.Load($"models/{ModelName}/B4LetterInside2.obj");

            DPadLeft1 = modelImporter.Load($"models/{ModelName}/DPadLeft1.obj");
            DPadUp2 = modelImporter.Load($"models/{ModelName}/DPadUp2.obj");
            DPadRight3 = modelImporter.Load($"models/{ModelName}/DPadRight3.obj");
            DPadDown4 = modelImporter.Load($"models/{ModelName}/DPadDown4.obj");

            // map model(s)
            foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
            {
                switch (button)
                {
                    case ButtonFlags.B1:
                    case ButtonFlags.B2:
                    case ButtonFlags.B3:
                    case ButtonFlags.B4:

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
            model3DGroup.Children.Add(MainBodyBack);

            model3DGroup.Children.Add(Smile1);
            model3DGroup.Children.Add(Smile2);
            model3DGroup.Children.Add(Smile3);
            model3DGroup.Children.Add(Smile4);
            model3DGroup.Children.Add(Smile5);
            model3DGroup.Children.Add(Smile6);

            model3DGroup.Children.Add(B1Letter);
            model3DGroup.Children.Add(B1LetterInside);
            model3DGroup.Children.Add(B2Letter);
            model3DGroup.Children.Add(B3Letter);
            model3DGroup.Children.Add(B3LetterInside);
            model3DGroup.Children.Add(B4Letter);
            model3DGroup.Children.Add(B4LetterInside1);
            model3DGroup.Children.Add(B4LetterInside2);

            model3DGroup.Children.Add(DPadLeft1);
            model3DGroup.Children.Add(DPadUp2);
            model3DGroup.Children.Add(DPadRight3);
            model3DGroup.Children.Add(DPadDown4);

            // specific button material(s)
            foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
            {
                int i = 0;
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ButtonFlags.B1:
                                buttonMaterial = MaterialPlasticOrange;
                                break;
                            case ButtonFlags.B2:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case ButtonFlags.B3:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case ButtonFlags.B4:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case ButtonFlags.DPadDown:
                            case ButtonFlags.DPadUp:
                            case ButtonFlags.DPadLeft:
                            case ButtonFlags.DPadRight:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case ButtonFlags.L2:
                                buttonMaterial = MaterialPlasticOrange;
                                break;
                            case ButtonFlags.R2:
                                buttonMaterial = MaterialPlasticPurple;
                                break;
                            case ButtonFlags.Start:
                            case ButtonFlags.Back:
                                buttonMaterial = MaterialPlasticYellow;
                                break;
                            case ButtonFlags.LeftThumb:
                            case ButtonFlags.RightThumb:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticBlack;
                                break;
                        }

                        DefaultMaterials[model3D] = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).Material = buttonMaterial;

                        i++;
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
                if (model3D.Equals(MainBody) || model3D.Equals(Smile1) || model3D.Equals(Smile2)
                    || model3D.Equals(DPadLeft1) || model3D.Equals(DPadUp2) || model3D.Equals(DPadRight3) || model3D.Equals(DPadDown4)
                    || model3D.Equals(B1Letter) || model3D.Equals(B2Letter) || model3D.Equals(B3Letter) || model3D.Equals(B4Letter)
                    )
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                if (model3D.Equals(MainBodyBack) || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreenFluorescent;
                    DefaultMaterials[model3D] = MaterialPlasticGreenFluorescent;
                }

                if (model3D.Equals(Smile3))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticRed;
                    DefaultMaterials[model3D] = MaterialPlasticRed;
                }


                if (model3D.Equals(JoystickLeftCover) || model3D.Equals(JoystickRightCover)
                    || model3D.Equals(LeftThumbRing) || model3D.Equals(RightThumbRing)
                    || model3D.Equals(B4LetterInside1) || model3D.Equals(B4LetterInside2))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlue;
                    DefaultMaterials[model3D] = MaterialPlasticBlue;
                }

                if (model3D.Equals(RightShoulderTrigger))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticPurple;
                    DefaultMaterials[model3D] = MaterialPlasticPurple;
                }

                if (model3D.Equals(LeftShoulderTrigger) || model3D.Equals(B1LetterInside))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticOrange;
                    DefaultMaterials[model3D] = MaterialPlasticOrange;
                }

                if (model3D.Equals(B3LetterInside))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreen;
                    DefaultMaterials[model3D] = MaterialPlasticGreen;
                }
            }

            base.DrawHighligths();
        }
    }
}
