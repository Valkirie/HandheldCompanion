using SharpDX.XInput;
using System;
using System.IO;
using System.Windows;
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
        Model3DGroup LogoInner;
        Model3DGroup LogoOuter;
        Model3DGroup MainBodyBack;
        Model3DGroup MainBodyTop;
        Model3DGroup MainBodySide;
        Model3DGroup ShareButton;
        Model3DGroup ShareButtonSymbol;
        Model3DGroup StartSymbol;
        Model3DGroup USBPortInner;
        Model3DGroup USBPortOuter;

        Model3DGroup AInterior;
        Model3DGroup AInterior2;
        Model3DGroup AButton;
        Model3DGroup BInterior;
        Model3DGroup BInterior2;
        Model3DGroup BButton;
        Model3DGroup XInterior;
        Model3DGroup XInterior2;
        Model3DGroup XButton;
        Model3DGroup YInterior;
        Model3DGroup YInterior2;
        Model3DGroup YButton;

        public ModelXBOXOne() : base("XBOXOne")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#1C1D21");
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
            LogoInner= modelImporter.Load($"models/{ModelName}/LogoInner.obj");
            LogoOuter= modelImporter.Load($"models/{ModelName}/LogoOuter.obj");
            MainBodyBack= modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            MainBodyTop= modelImporter.Load($"models/{ModelName}/MainBodyTop.obj");
            MainBodySide= modelImporter.Load($"models/{ModelName}/MainBodySide.obj");
            ShareButton = modelImporter.Load($"models/{ModelName}/ShareButton.obj");
            ShareButtonSymbol = modelImporter.Load($"models/{ModelName}/ShareButtonSymbol.obj");
            StartSymbol = modelImporter.Load($"models/{ModelName}/StartSymbol.obj");
            USBPortInner = modelImporter.Load($"models/{ModelName}/USBPortInner.obj");
            USBPortOuter = modelImporter.Load($"models/{ModelName}/USBPortOuter.obj");

            AInterior = modelImporter.Load($"models/{ModelName}/A-Interior.obj");
            AInterior2 = modelImporter.Load($"models/{ModelName}/A-Interior2.obj");
            AButton = modelImporter.Load($"models/{ModelName}/A-Button.obj");
            BInterior = modelImporter.Load($"models/{ModelName}/B-Interior.obj");
            BInterior2 = modelImporter.Load($"models/{ModelName}/B-Interior2.obj");
            BButton = modelImporter.Load($"models/{ModelName}/B-Button.obj");
            XInterior = modelImporter.Load($"models/{ModelName}/X-Interior.obj");
            XInterior2 = modelImporter.Load($"models/{ModelName}/X-Interior2.obj");
            XButton = modelImporter.Load($"models/{ModelName}/X-Button.obj");
            YInterior = modelImporter.Load($"models/{ModelName}/Y-Interior.obj");
            YInterior2 = modelImporter.Load($"models/{ModelName}/Y-Interior2.obj");
            YButton = modelImporter.Load($"models/{ModelName}/Y-Button.obj");

            // pull models
            model3DGroup.Children.Add(BackSymbol);
            model3DGroup.Children.Add(BatteryDoor);
            model3DGroup.Children.Add(BatteryDoorInner);
            model3DGroup.Children.Add(LogoInner);
            model3DGroup.Children.Add(LogoOuter);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(MainBodyTop);
            model3DGroup.Children.Add(MainBodySide);
            model3DGroup.Children.Add(ShareButton);
            model3DGroup.Children.Add(ShareButtonSymbol);
            model3DGroup.Children.Add(StartSymbol);
            model3DGroup.Children.Add(USBPortInner);
            model3DGroup.Children.Add(USBPortOuter);

            model3DGroup.Children.Add(AInterior);
            model3DGroup.Children.Add(AInterior2);
            model3DGroup.Children.Add(AButton);
            model3DGroup.Children.Add(BInterior);
            model3DGroup.Children.Add(BInterior2);
            model3DGroup.Children.Add(BButton);
            model3DGroup.Children.Add(XInterior);
            model3DGroup.Children.Add(XInterior2);
            model3DGroup.Children.Add(XButton);
            model3DGroup.Children.Add(YInterior);
            model3DGroup.Children.Add(YInterior2);
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
                            case GamepadButtonFlags.Back:
                            case GamepadButtonFlags.Start:
                            case GamepadButtonFlags.LeftShoulder:
                            case GamepadButtonFlags.RightShoulder:
                                buttonMaterial = MaterialPlasticWhite;
                                break;
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
                if (model3D == USBPortOuter || model3D == LogoOuter
                    || model3D == AInterior || model3D == AInterior2
                    || model3D == BInterior || model3D == BInterior2
                    || model3D == XInterior || model3D == XInterior2
                    || model3D == YInterior || model3D == YInterior2
                    || model3D == RightThumbRing || model3D == LeftThumbRing
                    || model3D == ShareButtonSymbol || model3D == StartSymbol || model3D == BackSymbol)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                    DefaultMaterials[model3D] = MaterialPlasticBlack;
                    continue;
                }

                // specific face button material
                if (model3D == AButton || model3D == BButton || model3D == XButton || model3D == YButton)
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
