using ControllerCommon.Controllers;
using SharpDX.XInput;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelXBOXOne : Model
    {
        // Specific models
        Model3DGroup BackSymbol;
        Model3DGroup BatteryDoor;
        Model3DGroup BatteryDoorInner;
        Model3DGroup SpecialOuter;
        Model3DGroup MainBodyBack;
        Model3DGroup MainBodyTop;
        Model3DGroup MainBodySide;
        Model3DGroup ShareButton;
        Model3DGroup ShareButtonSymbol;
        Model3DGroup StartSymbol;
        Model3DGroup USBPortInner;
        Model3DGroup USBPortOuter;

        Model3DGroup B1Interior;
        Model3DGroup B1Interior2;
        Model3DGroup B1Button;
        Model3DGroup B2Interior;
        Model3DGroup B2Interior2;
        Model3DGroup B2Button;
        Model3DGroup B3Interior;
        Model3DGroup B3Interior2;
        Model3DGroup B3Button;
        Model3DGroup B4Interior;
        Model3DGroup B4Interior2;
        Model3DGroup B4Button;

        public ModelXBOXOne() : base("XBOXOne")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#26272C");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#D8D7DC");

            var ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#E4D70E");
            var ColorPlasticGreen = (Color)ColorConverter.ConvertFromString("#76BA58");
            var ColorPlasticRed = (Color)ColorConverter.ConvertFromString("#FA3D45");
            var ColorPlasticBlue = (Color)ColorConverter.ConvertFromString("#119AE5");

            var ColorPlasticTransparent = (Color)ColorConverter.ConvertFromString("#232323");
            ColorPlasticTransparent.A = 50;

            // materials
            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

            var MaterialPlasticYellow = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellow));
            var MaterialPlasticGreen = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreen));
            var MaterialPlasticRed = new DiffuseMaterial(new SolidColorBrush(ColorPlasticRed));
            var MaterialPlasticBlue = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlue));

            var MaterialPlasticTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticTransparent));

            // rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-39.0f, -8.0f, 22.2f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(20.0f, -8.0f, -1.1f);
            JoystickMaxAngleDeg = 17.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-44.668f, 3.087f, 39.705);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(44.668f, 3.087f, 39.705);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-28.7f, -20.3f, 52.8f);
            UpwardVisibilityRotationPointRight = new Vector3D(28.7f, -20.3f, 52.8f);

            // load models
            BackSymbol = modelImporter.Load($"models/{ModelName}/BackSymbol.obj");
            BatteryDoor = modelImporter.Load($"models/{ModelName}/BatteryDoor.obj");
            BatteryDoorInner = modelImporter.Load($"models/{ModelName}/BatteryDoorInner.obj");
            SpecialOuter = modelImporter.Load($"models/{ModelName}/SpecialOuter.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            MainBodyTop = modelImporter.Load($"models/{ModelName}/MainBodyTop.obj");
            MainBodySide = modelImporter.Load($"models/{ModelName}/MainBodySide.obj");
            ShareButton = modelImporter.Load($"models/{ModelName}/ShareButton.obj");
            ShareButtonSymbol = modelImporter.Load($"models/{ModelName}/ShareButtonSymbol.obj");
            StartSymbol = modelImporter.Load($"models/{ModelName}/StartSymbol.obj");
            USBPortInner = modelImporter.Load($"models/{ModelName}/USBPortInner.obj");
            USBPortOuter = modelImporter.Load($"models/{ModelName}/USBPortOuter.obj");

            B1Interior = modelImporter.Load($"models/{ModelName}/B1-Interior.obj");
            B1Interior2 = modelImporter.Load($"models/{ModelName}/B1-Interior2.obj");
            B1Button = modelImporter.Load($"models/{ModelName}/B1-Button.obj");

            B2Interior = modelImporter.Load($"models/{ModelName}/B2-Interior.obj");
            B2Interior2 = modelImporter.Load($"models/{ModelName}/B2-Interior2.obj");
            B2Button = modelImporter.Load($"models/{ModelName}/B2-Button.obj");

            B3Interior = modelImporter.Load($"models/{ModelName}/B3-Interior.obj");
            B3Interior2 = modelImporter.Load($"models/{ModelName}/B3-Interior2.obj");
            B3Button = modelImporter.Load($"models/{ModelName}/B3-Button.obj");

            B4Interior = modelImporter.Load($"models/{ModelName}/B4-Interior.obj");
            B4Interior2 = modelImporter.Load($"models/{ModelName}/B4-Interior2.obj");
            B4Button = modelImporter.Load($"models/{ModelName}/B4-Button.obj");

            /*
             * TODO: @CasperH can you please help me figure out which object is the xbox button and rename it to Special.obj
             * See XBOX360 changes I made, I believe they're fine
             */

            // pull models
            model3DGroup.Children.Add(BackSymbol);
            model3DGroup.Children.Add(BatteryDoor);
            model3DGroup.Children.Add(BatteryDoorInner);
            model3DGroup.Children.Add(SpecialOuter);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(MainBodyTop);
            model3DGroup.Children.Add(MainBodySide);
            model3DGroup.Children.Add(ShareButton);
            model3DGroup.Children.Add(ShareButtonSymbol);
            model3DGroup.Children.Add(StartSymbol);
            model3DGroup.Children.Add(USBPortInner);
            model3DGroup.Children.Add(USBPortOuter);

            model3DGroup.Children.Add(B1Interior);
            model3DGroup.Children.Add(B1Interior2);
            model3DGroup.Children.Add(B1Button);
            model3DGroup.Children.Add(B2Interior);
            model3DGroup.Children.Add(B2Interior2);
            model3DGroup.Children.Add(B2Button);
            model3DGroup.Children.Add(B3Interior);
            model3DGroup.Children.Add(B3Interior2);
            model3DGroup.Children.Add(B3Button);
            model3DGroup.Children.Add(B4Interior);
            model3DGroup.Children.Add(B4Interior2);
            model3DGroup.Children.Add(B4Button);

            ButtonMap[ControllerButtonFlags.Special].Add(SpecialOuter);

            // specific button material(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ControllerButtonFlags.Back:
                            case ControllerButtonFlags.Start:
                            case ControllerButtonFlags.LeftShoulder:
                            case ControllerButtonFlags.RightShoulder:
                                buttonMaterial = MaterialPlasticWhite;
                                break;
                            case ControllerButtonFlags.B1:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case ControllerButtonFlags.B2:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case ControllerButtonFlags.B3:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case ControllerButtonFlags.B4:
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

            // materials
            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // default material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticWhite;

                // specific material(s)
                if (model3D == USBPortOuter || model3D == SpecialOuter
                    || model3D == B1Interior || model3D == B1Interior2
                    || model3D == B2Interior || model3D == B2Interior2
                    || model3D == B3Interior || model3D == B3Interior2
                    || model3D == B4Interior || model3D == B4Interior2
                    || model3D == RightThumbRing || model3D == LeftThumbRing
                    || model3D == ShareButtonSymbol || model3D == StartSymbol || model3D == BackSymbol)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                    DefaultMaterials[model3D] = MaterialPlasticBlack;
                    continue;
                }

                // specific face button material
                if (model3D == B1Button || model3D == B2Button || model3D == B3Button || model3D == B4Button)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparent;
                    continue;
                }
            }

            DrawHighligths();
        }
    }
}
