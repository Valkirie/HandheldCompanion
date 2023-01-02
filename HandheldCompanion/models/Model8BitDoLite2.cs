using ControllerCommon.Controllers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class Model8BitDoLite2 : Model
    {
        // Specific groups (move me)
        Model3DGroup BodyBack;
        Model3DGroup ChargerConnector;
        Model3DGroup ChargerPort;
        Model3DGroup Logo;
        Model3DGroup Home;
        Model3DGroup JoystickLeftCover;
        Model3DGroup JoystickRightCover;
        Model3DGroup LED1;
        Model3DGroup LED2;
        Model3DGroup LED3;
        Model3DGroup LED4;
        Model3DGroup Pair;
        Model3DGroup Power;
        Model3DGroup Reset;
        Model3DGroup Star;
        Model3DGroup ShoulderRightMiddle;
        Model3DGroup ShoulderLeftMiddle;

        public Model8BitDoLite2() : base("8BitDoLite2")
        {
            // colors
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E1E0E6");
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#43424B");
            var ColorLED = (Color)ColorConverter.ConvertFromString("#487B40");
            var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];

            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticTransparentLED = new SpecularMaterial(new SolidColorBrush(ColorLED), 0.3);
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-38.0f, -2.4f, 1.15f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(18.6f, -2.4f, -17.2f);
            JoystickMaxAngleDeg = 20.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-34.00f, 5.0f, 23.2f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(34.00f, 5.0f, 23.2f);
            TriggerMaxAngleDeg = 10.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-34.90f, -5.0f, 28.0f);
            UpwardVisibilityRotationPointRight = new Vector3D(34.90f, -5.0f, 28.0f);

            // load model(s)
            BodyBack = modelImporter.Load($"models/{ModelName}/BodyBack.obj");
            ChargerConnector = modelImporter.Load($"models/{ModelName}/ChargerConnector.obj"); ;
            ChargerPort = modelImporter.Load($"models/{ModelName}/ChargerPort.obj");
            Logo = modelImporter.Load($"models/{ModelName}/Logo.obj");
            Home = modelImporter.Load($"models/{ModelName}/Home.obj");
            JoystickLeftCover = modelImporter.Load($"models/{ModelName}/JoystickLeftCover.obj");
            JoystickRightCover = modelImporter.Load($"models/{ModelName}/JoystickRightCover.obj");
            LED1 = modelImporter.Load($"models/{ModelName}/LED1.obj");
            LED2 = modelImporter.Load($"models/{ModelName}/LED2.obj");
            LED3 = modelImporter.Load($"models/{ModelName}/LED3.obj");
            LED4 = modelImporter.Load($"models/{ModelName}/LED4.obj");
            Pair = modelImporter.Load($"models/{ModelName}/Pair.obj");
            Power = modelImporter.Load($"models/{ModelName}/Power.obj");
            Reset = modelImporter.Load($"models/{ModelName}/Reset.obj");
            Star = modelImporter.Load($"models/{ModelName}/Star.obj");
            ShoulderRightMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
            ShoulderLeftMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");

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
            model3DGroup.Children.Add(BodyBack);
            model3DGroup.Children.Add(ChargerConnector);
            model3DGroup.Children.Add(ChargerPort);
            model3DGroup.Children.Add(Logo);
            model3DGroup.Children.Add(Home);
            model3DGroup.Children.Add(JoystickLeftCover);
            model3DGroup.Children.Add(JoystickRightCover);
            model3DGroup.Children.Add(LED1);
            model3DGroup.Children.Add(LED2);
            model3DGroup.Children.Add(LED3);
            model3DGroup.Children.Add(LED4);
            model3DGroup.Children.Add(Pair);
            model3DGroup.Children.Add(Power);
            model3DGroup.Children.Add(Reset);
            model3DGroup.Children.Add(Star);
            model3DGroup.Children.Add(ShoulderLeftMiddle);
            model3DGroup.Children.Add(ShoulderRightMiddle);


            // Colors buttons
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
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticWhite;
                                break;
                            case ControllerButtonFlags.B2:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticWhite;
                                break;
                            case ControllerButtonFlags.B3:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticWhite;
                                break;
                            case ControllerButtonFlags.B4:
                                buttonMaterial = i == 0 ? MaterialPlasticBlack : MaterialPlasticWhite;
                                break;
                            case ControllerButtonFlags.Start:
                            case ControllerButtonFlags.Back:
                                buttonMaterial = MaterialPlasticWhite;
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

            // Colors models
            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;

                // specific material(s)
                if (model3D.Equals(MainBody) || model3D.Equals(BodyBack)
                    || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor)
                    || model3D.Equals(Power)
                    || model3D.Equals(ShoulderLeftMiddle) || model3D.Equals(ShoulderRightMiddle))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }

                if (model3D.Equals(LED1))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentLED;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparentLED;
                    DefaultMaterials[model3D] = MaterialPlasticTransparentLED;
                    continue;
                }

            }

            DrawHighligths();
        }
    }
}
