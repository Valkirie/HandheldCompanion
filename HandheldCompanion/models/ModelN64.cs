using SharpDX.XInput;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelN64 : Model
    {
        // Specific groups (move me)
        Model3DGroup JoystickShield;
        Model3DGroup CDown;

        public ModelN64() : base("N64")
        {
            // colors
            var ColorPlasticGrey = (Color)ColorConverter.ConvertFromString("#B4B9C5");
            var ColorPlasticDarkGrey = (Color)ColorConverter.ConvertFromString("#7A7F8B");

            var ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#FFC000");
            var ColorPlasticGreen = (Color)ColorConverter.ConvertFromString("#018D4E");
            var ColorPlasticRed = (Color)ColorConverter.ConvertFromString("#ff5f4b");
            var ColorPlasticBlue = (Color)ColorConverter.ConvertFromString("#0257B2");

            // materials
            var MaterialPlasticDarkGrey = new DiffuseMaterial(new SolidColorBrush(ColorPlasticDarkGrey));
            var MaterialPlasticGrey = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGrey));

            var MaterialPlasticYellow = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellow));
            var MaterialPlasticGreen = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreen));
            var MaterialPlasticRed = new DiffuseMaterial(new SolidColorBrush(ColorPlasticRed));
            var MaterialPlasticBlue = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlue));

            // rotation points and axis
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(0.0f, -4.745f, -31.132f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(0.0f, 0.0f, 0.0f);
            JoystickMaxAngleDeg = 21.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(0.0f, 0.0f, 0.0f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(0.0f, 0.0f, 0.0f);
            TriggerMaxAngleDeg = 5.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-26.4f, -7.5f, 39.2f);
            UpwardVisibilityRotationPointRight = new Vector3D(26.4f, -7.5f, 39.2f);

            // load model(s)
            JoystickShield = modelImporter.Load($"models/{ModelName}/JoystickShield.obj");
            CDown = modelImporter.Load($"models/{ModelName}/CDown.obj");

            // pull model(s)
            model3DGroup.Children.Add(JoystickShield);
            model3DGroup.Children.Add(CDown);

            // specific button material(s)
            foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case GamepadButtonFlags.Start:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case GamepadButtonFlags.X:
                            case GamepadButtonFlags.Y:
                            case GamepadButtonFlags.Back:
                                buttonMaterial = MaterialPlasticYellow;
                                break;
                            case GamepadButtonFlags.A:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case GamepadButtonFlags.B:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case GamepadButtonFlags.DPadDown:
                            case GamepadButtonFlags.DPadUp:
                            case GamepadButtonFlags.DPadLeft:
                            case GamepadButtonFlags.DPadRight:
                            case GamepadButtonFlags.LeftShoulder:
                            case GamepadButtonFlags.RightShoulder:
                                buttonMaterial = MaterialPlasticDarkGrey;
                                break;
                            case GamepadButtonFlags.LeftThumb:
                            case GamepadButtonFlags.RightThumb:
                                buttonMaterial = MaterialPlasticGrey;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticGrey;
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

                // default material
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticDarkGrey;
                DefaultMaterials[model3D] = MaterialPlasticDarkGrey;

                // specific materials
                if (model3D == MainBody || model3D == LeftThumbRing)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGrey;
                    DefaultMaterials[model3D] = MaterialPlasticGrey;
                    continue;
                }

                if (model3D == CDown)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticYellow;
                    DefaultMaterials[model3D] = MaterialPlasticYellow;
                    continue;
                }
                
            }

            DrawHighligths();
        }
    }
}
