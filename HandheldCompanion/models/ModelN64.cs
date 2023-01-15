using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelN64 : IModel
    {
        // Specific groups (move me)
        Model3DGroup JoystickShield;

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
            // todo : add all four c-buttons

            // pull model(s)
            model3DGroup.Children.Add(JoystickShield);

            // specific button material(s)
            foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ButtonFlags.Start:
                                buttonMaterial = MaterialPlasticRed;
                                break;
                            case ButtonFlags.B3:
                            case ButtonFlags.B4:
                            case ButtonFlags.Back:
                            case ButtonFlags.RStickDown:
                            case ButtonFlags.RStickLeft:
                            case ButtonFlags.RStickRight:
                            case ButtonFlags.RStickUp:
                                buttonMaterial = MaterialPlasticYellow;
                                break;
                            case ButtonFlags.B1:
                                buttonMaterial = MaterialPlasticBlue;
                                break;
                            case ButtonFlags.B2:
                                buttonMaterial = MaterialPlasticGreen;
                                break;
                            case ButtonFlags.DPadDown:
                            case ButtonFlags.DPadUp:
                            case ButtonFlags.DPadLeft:
                            case ButtonFlags.DPadRight:
                            case ButtonFlags.L2:
                            case ButtonFlags.R2:
                                buttonMaterial = MaterialPlasticDarkGrey;
                                break;
                            case ButtonFlags.LeftThumb:
                            case ButtonFlags.RightThumb:
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
                if (model3D.Equals(MainBody) || model3D.Equals(LeftThumbRing))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGrey;
                    DefaultMaterials[model3D] = MaterialPlasticGrey;
                    continue;
                }
            }

            base.DrawHighligths();
        }
    }
}
