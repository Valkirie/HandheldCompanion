using ControllerCommon.Controllers;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelAYANEOAir : IModel
    {
        // Specific groups (move me)
        Model3DGroup AudioJack;
        Model3DGroup DPadTriangle;
        Model3DGroup JoystickCoverLeft;
        Model3DGroup JoystickCoverRight;
        Model3DGroup JoystickLEDLeft;
        Model3DGroup JoystickLEDRight;
        Model3DGroup JoystickPCBLeft;
        Model3DGroup JoystickPCBRight;
        Model3DGroup MainBodyBack;
        Model3DGroup MicroSD;
        Model3DGroup PowerButton;
        Model3DGroup ShoulderMiddleLeft;
        Model3DGroup ShoulderMiddleRight;
        Model3DGroup Screen;
        Model3DGroup Screw1;
        Model3DGroup Screw2;
        Model3DGroup TopBar;
        Model3DGroup VolumeButton;

        Model3DGroup ALetter;
        Model3DGroup BLetter;
        Model3DGroup XLetter;
        Model3DGroup YLetter;

        public ModelAYANEOAir() : base("AYANEO Air")
        {
            // material and colors

            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#262628");
            var ColorPlasticGreyLight = (Color)ColorConverter.ConvertFromString("#B7B8BD");
            var ColorPlasticGreyDark = (Color)ColorConverter.ConvertFromString("#59585E");
            var ColorPlasticYellow = (Color)ColorConverter.ConvertFromString("#F9D936");

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticGreyLight = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreyLight));
            var MaterialPlasticGreyDark = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGreyDark));
            var MaterialPlasticYellow = new DiffuseMaterial(new SolidColorBrush(ColorPlasticYellow));
            var MaterialPlasticTransparentHighlight = new SpecularMaterial(new SolidColorBrush(ColorPlasticYellow), 0.8);

            // rotation points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-88.9f, -6.7f, 31.2f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(89.0f, -6.7f, 2.76f);
            JoystickMaxAngleDeg = 17.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-81.0f, 2.5f, 45.8f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(81.0f, 2.5f, 45.8f);
            TriggerMaxAngleDeg = 22.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(26.915, 0, 7.27);
            UpwardVisibilityRotationAxisRight = new Vector3D(26.915, 0, -7.27);
            UpwardVisibilityRotationPointLeft = new Vector3D(-80.0f, -10.0f, 51.8f);
            UpwardVisibilityRotationPointRight = new Vector3D(80.0f, -10.0f, 51.8f);

            // load model(s)
            AudioJack = modelImporter.Load($"models/{ModelName}/AudioJack.obj");
            DPadTriangle = modelImporter.Load($"models/{ModelName}/DPadTriangle.obj");
            JoystickCoverLeft = modelImporter.Load($"models/{ModelName}/JoystickCoverLeft.obj");
            JoystickCoverRight = modelImporter.Load($"models/{ModelName}/JoystickCoverRight.obj");
            JoystickLEDLeft = modelImporter.Load($"models/{ModelName}/JoystickLeftLED.obj");
            JoystickLEDRight = modelImporter.Load($"models/{ModelName}/JoystickRightLED.obj");
            JoystickPCBLeft = modelImporter.Load($"models/{ModelName}/JoystickPCBLeft.obj");
            JoystickPCBRight = modelImporter.Load($"models/{ModelName}/JoystickPCBRight.obj");
            ShoulderMiddleLeft = modelImporter.Load($"models/{ModelName}/ShoulderMiddleLeft.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            MicroSD = modelImporter.Load($"models/{ModelName}/MicroSD.obj");
            PowerButton = modelImporter.Load($"models/{ModelName}/PowerButton.obj");
            ShoulderMiddleRight = modelImporter.Load($"models/{ModelName}/ShoulderMiddleRight.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            Screw1 = modelImporter.Load($"models/{ModelName}/Screw1.obj");
            Screw2 = modelImporter.Load($"models/{ModelName}/Screw2.obj");
            TopBar = modelImporter.Load($"models/{ModelName}/TopBar.obj");
            VolumeButton = modelImporter.Load($"models/{ModelName}/VolumeButton.obj");

            ALetter = modelImporter.Load($"models/{ModelName}/B1-Letter.obj");
            BLetter = modelImporter.Load($"models/{ModelName}/B2-Letter.obj");
            XLetter = modelImporter.Load($"models/{ModelName}/B3-Letter.obj");
            YLetter = modelImporter.Load($"models/{ModelName}/B4-Letter.obj");

            // pull model(s
            model3DGroup.Children.Add(AudioJack);
            model3DGroup.Children.Add(DPadTriangle);
            model3DGroup.Children.Add(JoystickCoverLeft);
            model3DGroup.Children.Add(JoystickCoverRight);
            model3DGroup.Children.Add(JoystickLEDLeft);
            model3DGroup.Children.Add(JoystickLEDRight);
            model3DGroup.Children.Add(JoystickPCBLeft);
            model3DGroup.Children.Add(JoystickPCBRight);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(MicroSD);
            model3DGroup.Children.Add(PowerButton);
            model3DGroup.Children.Add(ShoulderMiddleLeft);
            model3DGroup.Children.Add(ShoulderMiddleRight);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(Screw1);
            model3DGroup.Children.Add(Screw2);
            model3DGroup.Children.Add(TopBar);
            model3DGroup.Children.Add(VolumeButton);

            model3DGroup.Children.Add(ALetter);
            model3DGroup.Children.Add(BLetter);
            model3DGroup.Children.Add(XLetter);
            model3DGroup.Children.Add(YLetter);

            // specific button material(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ControllerButtonFlags.B1:
                            case ControllerButtonFlags.B2:
                            case ControllerButtonFlags.B3:
                            case ControllerButtonFlags.B4:
                            case ControllerButtonFlags.DPadDown:
                            case ControllerButtonFlags.DPadUp:
                            case ControllerButtonFlags.DPadLeft:
                            case ControllerButtonFlags.DPadRight:
                            case ControllerButtonFlags.LeftShoulder:
                            case ControllerButtonFlags.RightShoulder:
                            case ControllerButtonFlags.LeftThumb:
                            case ControllerButtonFlags.RightThumb:
                            case ControllerButtonFlags.OEM3:
                            case ControllerButtonFlags.OEM4:
                                buttonMaterial = MaterialPlasticGreyDark;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticGreyLight;
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

                // default material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreyLight;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticGreyLight;
                DefaultMaterials[model3D] = MaterialPlasticGreyLight;

                // specific material(s)
                // black
                if (model3D.Equals(Screen))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                    DefaultMaterials[model3D] = MaterialPlasticBlack;
                    continue;
                }

                // dark grey
                if (model3D.Equals(TopBar) || model3D.Equals(PowerButton)
                    || model3D.Equals(ShoulderMiddleLeft) || model3D.Equals(ShoulderMiddleRight)
                    || model3D.Equals(LeftShoulderTrigger) || model3D.Equals(RightShoulderTrigger)
                    || model3D.Equals(LeftThumbRing) || model3D.Equals(RightThumbRing)
                    || model3D.Equals(JoystickCoverLeft) || model3D.Equals(JoystickCoverRight)
                    || model3D.Equals(LeftThumb) || model3D.Equals(RightThumb)
                    || model3D.Equals(JoystickPCBLeft) || model3D.Equals(JoystickPCBRight)
                    )
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGreyDark;
                    DefaultMaterials[model3D] = MaterialPlasticGreyDark;
                    continue;
                }

                // yellow
                if (model3D.Equals(DPadTriangle) || model3D.Equals(ALetter) || model3D.Equals(BLetter) || model3D.Equals(XLetter) || model3D.Equals(YLetter))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticYellow;
                    DefaultMaterials[model3D] = MaterialPlasticYellow;
                    continue;
                }

                // LED
                if (model3D.Equals(JoystickLEDRight) || model3D.Equals(JoystickLEDLeft))
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentHighlight;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticYellow;
                    DefaultMaterials[model3D] = MaterialPlasticTransparentHighlight;
                    continue;
                }
            }

            DrawHighligths();
        }

        // highlight material
        private new void DrawHighligths()
        {
            var ColorHighlight = (Color)ColorConverter.ConvertFromString("#F9D936");
            var MaterialHighlight = new DiffuseMaterial(new SolidColorBrush(ColorHighlight));

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
