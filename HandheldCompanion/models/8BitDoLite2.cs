using SharpDX.XInput;
using System;
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
            var ColorPlasticTurquoise = (Color)ColorConverter.ConvertFromString("#29C5CA");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#E2E2E2");
            var ColorLED = (Color)ColorConverter.ConvertFromString("#487B40");

            var MaterialPlasticTurquoise = new DiffuseMaterial(new SolidColorBrush(ColorPlasticTurquoise));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialPlasticTransparentLED = new SpecularMaterial(new SolidColorBrush(ColorLED), 0.3);

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
            BodyBack= modelImporter.Load($"models/{ModelName}/BodyBack.obj");
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
            foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case GamepadButtonFlags.Start:
                            case GamepadButtonFlags.Back:
                                buttonMaterial = MaterialPlasticTurquoise;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticWhite;
                                break;
                        }

                        DefaultMaterials[model3D] = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).Material = buttonMaterial;
                    }
            }

            // Colors models
            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // generic material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                DefaultMaterials[model3D] = MaterialPlasticWhite;

                // specific material(s)
                if (model3D == MainBody || model3D == BodyBack
                    || model3D == LeftMotor || model3D == RightMotor
                    || model3D == Power
                    || model3D == ShoulderLeftMiddle || model3D == ShoulderRightMiddle)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTurquoise;
                    DefaultMaterials[model3D] = MaterialPlasticTurquoise;
                    continue;
                }

                if (model3D == LED1)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentLED;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparentLED;
                    DefaultMaterials[model3D] = MaterialPlasticTransparentLED;
                    continue;
                }

            }

            DrawHighligths();
        }

        private new void DrawHighligths()
        {
            var ColorHighlight = (Color)ColorConverter.ConvertFromString("#9f9f9f");
            var MaterialHighlight = new DiffuseMaterial(new SolidColorBrush(ColorHighlight));

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
