using HandheldCompanion.Inputs;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;


namespace HandheldCompanion.Models;

internal class ModelDualSense : IModel
{
    // Specific groups
    private readonly Model3DGroup AudioJack;

    private readonly Model3DGroup B1Button;
    private readonly Model3DGroup B1ButtonSymbolx;
    private readonly Model3DGroup B2Button;
    private readonly Model3DGroup B2ButtonSymbolx;
    private readonly Model3DGroup B3Button;
    private readonly Model3DGroup B3ButtonSymbolx;
    private readonly Model3DGroup B4Button;
    private readonly Model3DGroup B4ButtonSymbolx;
    private readonly Model3DGroup Charger;
    private readonly Model3DGroup DPadDownArrow;

    private readonly Model3DGroup DPadDownCover;
    private readonly Model3DGroup DPadLeftArrow;
    private readonly Model3DGroup DPadLeftCover;
    private readonly Model3DGroup DPadRightArrow;
    private readonly Model3DGroup DPadRightCover;
    private readonly Model3DGroup DPadUpArrow;
    private readonly Model3DGroup DPadUpCover;
    private readonly Model3DGroup LED1;
    private readonly Model3DGroup LED2;
    private readonly Model3DGroup LED3;
    private readonly Model3DGroup MainBodyBack;
    private readonly Model3DGroup MainBodyFront;
    private readonly Model3DGroup MenuSymbol;
    private readonly Model3DGroup ShareSymbol;
    private readonly Model3DGroup Special;
    private readonly Model3DGroup USBPort;

    public ModelDualSense() : base("DualSense")
    {
        // colors
        var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#21242E");
        var ColorPlasticGrey = (Color)ColorConverter.ConvertFromString("#7C7F8C");
        var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#DADFE8");
        var ColorMetal = (Color)ColorConverter.ConvertFromString("#5A4928");
        var ColorLEDOff = (Color)ColorConverter.ConvertFromString("#35383E");

        var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];
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
        AudioJack = modelImporter.Load($"3DModels/{ModelName}/AudioJack.obj");
        Charger = modelImporter.Load($"3DModels/{ModelName}/Charger.obj");
        LED1 = modelImporter.Load($"3DModels/{ModelName}/LED1.obj");
        LED2 = modelImporter.Load($"3DModels/{ModelName}/LED2.obj");
        LED3 = modelImporter.Load($"3DModels/{ModelName}/LED3.obj");
        MainBodyBack = modelImporter.Load($"3DModels/{ModelName}/MainBodyBack.obj");
        MainBodyFront = modelImporter.Load($"3DModels/{ModelName}/MainBodyFront.obj");
        Special = modelImporter.Load($"3DModels/{ModelName}/Special.obj");
        USBPort = modelImporter.Load($"3DModels/{ModelName}/USBPort.obj");
        ShareSymbol = modelImporter.Load($"3DModels/{ModelName}/ShareSymbol.obj");
        MenuSymbol = modelImporter.Load($"3DModels/{ModelName}/MenuSymbol.obj");

        DPadDownCover = modelImporter.Load($"3DModels/{ModelName}/DPadDownCover.obj");
        DPadUpCover = modelImporter.Load($"3DModels/{ModelName}/DPadUpCover.obj");
        DPadLeftCover = modelImporter.Load($"3DModels/{ModelName}/DPadLeftCover.obj");
        DPadRightCover = modelImporter.Load($"3DModels/{ModelName}/DPadRightCover.obj");
        DPadDownArrow = modelImporter.Load($"3DModels/{ModelName}/DPadDownArrow.obj");
        DPadUpArrow = modelImporter.Load($"3DModels/{ModelName}/DPadUpArrow.obj");
        DPadLeftArrow = modelImporter.Load($"3DModels/{ModelName}/DPadLeftArrow.obj");
        DPadRightArrow = modelImporter.Load($"3DModels/{ModelName}/DPadRightArrow.obj");

        B1Button = modelImporter.Load($"3DModels/{ModelName}/B1Button.obj");
        B2Button = modelImporter.Load($"3DModels/{ModelName}/B2Button.obj");
        B3Button = modelImporter.Load($"3DModels/{ModelName}/B3Button.obj");
        B4Button = modelImporter.Load($"3DModels/{ModelName}/B4Button.obj");
        B1ButtonSymbolx = modelImporter.Load($"3DModels/{ModelName}/B1ButtonSymbol.obj");
        B2ButtonSymbolx = modelImporter.Load($"3DModels/{ModelName}/B2ButtonSymbol.obj");
        B3ButtonSymbolx = modelImporter.Load($"3DModels/{ModelName}/B3ButtonSymbol.obj");
        B4ButtonSymbolx = modelImporter.Load($"3DModels/{ModelName}/B4ButtonSymbol.obj");

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

        model3DGroup.Children.Add(DPadDownArrow);
        model3DGroup.Children.Add(DPadUpArrow);
        model3DGroup.Children.Add(DPadLeftArrow);
        model3DGroup.Children.Add(DPadRightArrow);
        model3DGroup.Children.Add(DPadDownCover);
        model3DGroup.Children.Add(DPadUpCover);
        model3DGroup.Children.Add(DPadLeftCover);
        model3DGroup.Children.Add(DPadRightCover);

        model3DGroup.Children.Add(B1ButtonSymbolx);
        model3DGroup.Children.Add(B2ButtonSymbolx);
        model3DGroup.Children.Add(B3ButtonSymbolx);
        model3DGroup.Children.Add(B4ButtonSymbolx);
        model3DGroup.Children.Add(B1Button);
        model3DGroup.Children.Add(B2Button);
        model3DGroup.Children.Add(B3Button);
        model3DGroup.Children.Add(B4Button);


        // specific button material(s)
        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
        {
            Material buttonMaterial = null;

            if (ButtonMap.TryGetValue(button, out var map))
                foreach (var model3D in map)
                {
                    switch (button)
                    {
                        case ButtonFlags.LeftStickClick:
                        case ButtonFlags.RightStickClick:
                        case ButtonFlags.L1:
                        case ButtonFlags.L2Soft:
                        case ButtonFlags.R1:
                        case ButtonFlags.R2Soft:
                        case ButtonFlags.Special:
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
            if (model3D.Equals(MainBodyFront) || model3D.Equals(Special)
                                              || model3D.Equals(AudioJack) || model3D.Equals(USBPort)
                                              || model3D.Equals(LeftThumbRing) || model3D.Equals(RightThumbRing)
                                              || model3D.Equals(LeftShoulderTrigger) ||
                                              model3D.Equals(RightShoulderTrigger))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;
                continue;
            }

            if (model3D.Equals(ShareSymbol) || model3D.Equals(MenuSymbol)
                                            || model3D.Equals(DPadUpArrow) || model3D.Equals(DPadRightArrow) ||
                                            model3D.Equals(DPadDownArrow) || model3D.Equals(DPadLeftArrow)
                                            || model3D.Equals(B1ButtonSymbolx) || model3D.Equals(B2ButtonSymbolx) ||
                                            model3D.Equals(B3ButtonSymbolx) || model3D.Equals(B4ButtonSymbolx))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticGrey;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticGrey;
                DefaultMaterials[model3D] = MaterialPlasticGrey;
                continue;
            }

            if (model3D.Equals(LED1) || model3D.Equals(LED2))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialHighlight;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialHighlight;
                DefaultMaterials[model3D] = MaterialHighlight;
                continue;
            }

            if (model3D.Equals(Charger))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialMetal;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialMetal;
                DefaultMaterials[model3D] = MaterialMetal;
                continue;
            }

            if (model3D.Equals(LED3))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialLEDOff;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialLEDOff;
                DefaultMaterials[model3D] = MaterialLEDOff;
                continue;
            }

            if (model3D.Equals(DPadDownCover) || model3D.Equals(DPadUpCover) || model3D.Equals(DPadLeftCover) ||
                model3D.Equals(DPadRightCover)
                || model3D.Equals(B1Button) || model3D.Equals(B2Button) || model3D.Equals(B3Button) ||
                model3D.Equals(B4Button)
               )
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparent;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparent;
                DefaultMaterials[model3D] = MaterialPlasticTransparent;
            }
        }

        base.DrawAccentHighligths();
    }
}