using ControllerCommon.Controllers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelDS4 : IModel
    {
        // Specific groups (move me)
        Model3DGroup LeftShoulderMiddle;
        Model3DGroup RightShoulderMiddle;
        Model3DGroup Screen;
        Model3DGroup MainBodyBack;
        Model3DGroup AuxPort;
        Model3DGroup Triangle;
        Model3DGroup DPadDownArrow;
        Model3DGroup DPadUpArrow;
        Model3DGroup DPadLeftArrow;
        Model3DGroup DPadRightArrow;

        public ModelDS4() : base("DS4")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#38383A");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E0E0E0");

            var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

            var MaterialPlasticTriangle = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66a0a4")));
            var MaterialPlasticCross = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96b2d9")));
            var MaterialPlasticCircle = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d66673")));
            var MaterialPlasticSquare = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d7bee5")));
            var MaterialPlasticTransparent = new SpecularMaterial();

            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-25.5f, -5.086f, -21.582f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(25.5f, -5.086f, -21.582f);
            JoystickMaxAngleDeg = 19.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-38.061f, 3.09f, 26.842f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(38.061f, 3.09f, 26.842f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-48.868f, -13f, 29.62f);
            UpwardVisibilityRotationPointRight = new Vector3D(48.868f, -13f, 29.62f);

            // load model(s)
            LeftShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
            RightShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            AuxPort = modelImporter.Load($"models/{ModelName}/Aux-Port.obj");
            Triangle = modelImporter.Load($"models/{ModelName}/Triangle.obj");
            DPadDownArrow = modelImporter.Load($"models/{ModelName}/DPadDownArrow.obj");
            DPadUpArrow = modelImporter.Load($"models/{ModelName}/DPadUpArrow.obj");
            DPadLeftArrow = modelImporter.Load($"models/{ModelName}/DPadLeftArrow.obj");
            DPadRightArrow = modelImporter.Load($"models/{ModelName}/DPadRightArrow.obj");

            // map model(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                switch (button)
                {
                    case ControllerButtonFlags.B1:
                    case ControllerButtonFlags.B2:
                    case ControllerButtonFlags.B3:
                    case ControllerButtonFlags.B4:

                        string filename = $"models/{ModelName}/{button}-Symbol.obj";
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
            model3DGroup.Children.Add(LeftShoulderMiddle);
            model3DGroup.Children.Add(RightShoulderMiddle);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(AuxPort);
            model3DGroup.Children.Add(Triangle);
            model3DGroup.Children.Add(DPadDownArrow);
            model3DGroup.Children.Add(DPadUpArrow);
            model3DGroup.Children.Add(DPadLeftArrow);
            model3DGroup.Children.Add(DPadRightArrow);

            // specific button material(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                int i = 0;
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ControllerButtonFlags.B1:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticCross;
                                break;
                            case ControllerButtonFlags.B2:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticCircle;
                                break;
                            case ControllerButtonFlags.B3:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticSquare;
                                break;
                            case ControllerButtonFlags.B4:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticTriangle;
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

                // specific material(s)
                if (model3D.Equals(MainBody) || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor) || model3D.Equals(Triangle))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;
            }

            base.DrawAccentHighligths();
        }
    }
}
