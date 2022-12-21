using ControllerCommon.Controllers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelDualSense : Model
    {
        // Specific groups
        Model3DGroup AudioJack;
        Model3DGroup Charger;
        Model3DGroup LED1;
        Model3DGroup LED2;
        Model3DGroup LED3;
        Model3DGroup MainBodyBack;
        Model3DGroup MainBodyFront;
        Model3DGroup Special;
        Model3DGroup USBPort;
        Model3DGroup ShareSymbol;
        Model3DGroup MenuSymbol;

        Model3DGroup DPadDownCover;
        Model3DGroup DPadUpCover;
        Model3DGroup DPadLeftCover;
        Model3DGroup DPadRightCover;
        Model3DGroup DPadDownArrow;
        Model3DGroup DPadUpArrow;
        Model3DGroup DPadLeftArrow;
        Model3DGroup DPadRightArrow;

        Model3DGroup B1Button;
        Model3DGroup B2Button;
        Model3DGroup B3Button;
        Model3DGroup B4Button;
        Model3DGroup B1ButtonSymbolx;
        Model3DGroup B2ButtonSymbolx;
        Model3DGroup B3ButtonSymbolx;
        Model3DGroup B4ButtonSymbolx;

        public ModelDualSense() : base("DualSense")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#21242E");
            var ColorPlasticGrey = (Color)ColorConverter.ConvertFromString("#7C7F8C");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#DADFE8");
            var ColorMetal = (Color)ColorConverter.ConvertFromString("#5A4928");
            var ColorLEDOff = (Color)ColorConverter.ConvertFromString("#35383E");

            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
            var ColorPlasticTransparent = ColorPlasticWhite;
            byte TransparancyAmount = 100;
            ColorPlasticTransparent.A = TransparancyAmount;

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticGrey = new DiffuseMaterial(new SolidColorBrush(ColorPlasticGrey));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            var MaterialMetal = new DiffuseMaterial(new SolidColorBrush(ColorMetal));
            var MaterialLEDOff = new DiffuseMaterial(new SolidColorBrush(ColorLEDOff));

            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);
            var MaterialPlasticTransparent = new DiffuseMaterial(new SolidColorBrush(ColorPlasticTransparent));

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-30.339f, -10.7f, -1.507f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(30.339f, -10.7f, -1.507f);
            JoystickMaxAngleDeg = 14.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-65.4f, -0.64f, 45.8f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(65.4f, -0.64f, 45.8f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-60.83f, -26.2f, 60.9f);
            UpwardVisibilityRotationPointRight = new Vector3D(60.83f, -26.2f, 60.9f);

            // load model(s)
            AudioJack = modelImporter.Load($"models/{ModelName}/AudioJack.obj");
            Charger = modelImporter.Load($"models/{ModelName}/Charger.obj");
            LED1 = modelImporter.Load($"models/{ModelName}/LED1.obj");
            LED2 = modelImporter.Load($"models/{ModelName}/LED2.obj");
            LED3 = modelImporter.Load($"models/{ModelName}/LED3.obj");
            MainBodyBack = modelImporter.Load($"models/{ModelName}/MainBodyBack.obj");
            MainBodyFront = modelImporter.Load($"models/{ModelName}/MainBodyFront.obj");
            Special = modelImporter.Load($"models/{ModelName}/Special.obj");
            USBPort = modelImporter.Load($"models/{ModelName}/USBPort.obj");
            ShareSymbol = modelImporter.Load($"models/{ModelName}/ShareSymbol.obj");
            MenuSymbol = modelImporter.Load($"models/{ModelName}/MenuSymbol.obj");

            DPadDownCover = modelImporter.Load($"models/{ModelName}/DPadDownCover.obj");
            DPadUpCover = modelImporter.Load($"models/{ModelName}/DPadUpCover.obj");
            DPadLeftCover = modelImporter.Load($"models/{ModelName}/DPadLeftCover.obj");
            DPadRightCover = modelImporter.Load($"models/{ModelName}/DPadRightCover.obj");
            DPadDownArrow = modelImporter.Load($"models/{ModelName}/DPadDownArrow.obj");
            DPadUpArrow = modelImporter.Load($"models/{ModelName}/DPadUpArrow.obj");
            DPadLeftArrow = modelImporter.Load($"models/{ModelName}/DPadLeftArrow.obj");
            DPadRightArrow = modelImporter.Load($"models/{ModelName}/DPadRightArrow.obj");

            B1Button = modelImporter.Load($"models/{ModelName}/B1Button.obj");
            B2Button = modelImporter.Load($"models/{ModelName}/B2Button.obj");
            B3Button = modelImporter.Load($"models/{ModelName}/B3Button.obj");
            B4Button = modelImporter.Load($"models/{ModelName}/B4Button.obj");
            B1ButtonSymbolx = modelImporter.Load($"models/{ModelName}/B1ButtonSymbol.obj");
            B2ButtonSymbolx = modelImporter.Load($"models/{ModelName}/B2ButtonSymbol.obj");
            B3ButtonSymbolx = modelImporter.Load($"models/{ModelName}/B3ButtonSymbol.obj");
            B4ButtonSymbolx = modelImporter.Load($"models/{ModelName}/B4ButtonSymbol.obj");

            // pull model(s)
            model3DGroup.Children.Add(AudioJack);
            model3DGroup.Children.Add(Charger);
            model3DGroup.Children.Add(LED1);
            model3DGroup.Children.Add(LED2);
            model3DGroup.Children.Add(LED3);
            model3DGroup.Children.Add(MainBodyBack);
            model3DGroup.Children.Add(MainBodyFront);
            model3DGroup.Children.Add(USBPort);
            model3DGroup.Children.Add(ShareSymbol);
            model3DGroup.Children.Add(MenuSymbol);

            model3DGroup.Children.Add(DPadDownCover);
            model3DGroup.Children.Add(DPadUpCover);
            model3DGroup.Children.Add(DPadLeftCover);
            model3DGroup.Children.Add(DPadRightCover);
            model3DGroup.Children.Add(DPadDownArrow);
            model3DGroup.Children.Add(DPadUpArrow);
            model3DGroup.Children.Add(DPadLeftArrow);
            model3DGroup.Children.Add(DPadRightArrow);

            model3DGroup.Children.Add(B1Button);
            model3DGroup.Children.Add(B2Button);
            model3DGroup.Children.Add(B3Button);
            model3DGroup.Children.Add(B4Button);
            model3DGroup.Children.Add(B1ButtonSymbolx);
            model3DGroup.Children.Add(B2ButtonSymbolx);
            model3DGroup.Children.Add(B3ButtonSymbolx);
            model3DGroup.Children.Add(B4ButtonSymbolx);

            // specific button material(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ControllerButtonFlags.LeftThumb:
                            case ControllerButtonFlags.RightThumb:
                            case ControllerButtonFlags.LeftShoulder:
                            case ControllerButtonFlags.RightShoulder:
                            case ControllerButtonFlags.Special:
                                buttonMaterial = MaterialPlasticBlack;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticWhite;
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
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticWhite;

                // specific material(s)
                if (model3D == MainBodyFront || model3D == Special
                    || model3D == AudioJack || model3D == USBPort
                    || model3D == LeftThumbRing || model3D == RightThumbRing
                    || model3D == LeftShoulderTrigger || model3D == RightShoulderTrigger)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                    DefaultMaterials[model3D] = MaterialPlasticBlack;
                    continue;
                }

                if (model3D == ShareSymbol || model3D == MenuSymbol
                    || model3D == DPadUpArrow || model3D == DPadRightArrow || model3D == DPadDownArrow || model3D == DPadLeftArrow
                    || model3D == B1ButtonSymbolx || model3D == B2ButtonSymbolx || model3D == B3ButtonSymbolx || model3D == B4ButtonSymbolx)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGrey;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticGrey;
                    DefaultMaterials[model3D] = MaterialPlasticGrey;
                    continue;
                }

                if (model3D == LED1 || model3D == LED2)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialHighlight;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialHighlight;
                    DefaultMaterials[model3D] = MaterialHighlight;
                    continue;
                }

                if (model3D == Charger)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialMetal;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialMetal;
                    DefaultMaterials[model3D] = MaterialMetal;
                    continue;
                }

                if (model3D == LED3)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialLEDOff;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialLEDOff;
                    DefaultMaterials[model3D] = MaterialLEDOff;
                    continue;
                }

                if (model3D == DPadDownCover || model3D == DPadUpCover || model3D == DPadLeftCover || model3D == DPadRightCover
                  || model3D == B1Button || model3D == B2Button || model3D == B3Button || model3D == B4Button
                    )
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparent;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparent;
                    DefaultMaterials[model3D] = MaterialPlasticTransparent;
                    continue;
                }

            }

            DrawHighligths();
        }

        private new void DrawHighligths()
        {
            var ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
