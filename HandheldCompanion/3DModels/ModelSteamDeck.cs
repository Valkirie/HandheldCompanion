using HandheldCompanion.Inputs;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;


namespace HandheldCompanion.Models;

internal class ModelSteamDeck : IModel
{
    private readonly Model3DGroup B1Symbol;
    private readonly Model3DGroup B2Symbol;
    private readonly Model3DGroup B3Symbol;

    private readonly Model3DGroup B4Symbol;

    // Specific groups
    private readonly Model3DGroup BackIcon;
    private readonly Model3DGroup MainBodyLeftOver;
    private readonly Model3DGroup PowerButton;
    private readonly Model3DGroup Screen;
    private readonly Model3DGroup StartIcon;
    private readonly Model3DGroup SteamText;
    private readonly Model3DGroup ThreeDots;
    private readonly Model3DGroup VolumeDown;
    private readonly Model3DGroup VolumeUp;

    public ModelSteamDeck() : base("SteamDeck")
    {
        // colors
        var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#4F4D50");
        var ColorPlasticDarkGrey = (Color)ColorConverter.ConvertFromString("#605F5D");
        var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#A7A5A6");

        var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
        var MaterialPlasticDarkGrey = new DiffuseMaterial(new SolidColorBrush(ColorPlasticDarkGrey));
        var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

        // Rotation Points
        JoystickRotationPointCenterLeftMillimeter = new Vector3D(-102.83f, 6.15f, 34.5f);
        JoystickRotationPointCenterRightMillimeter = new Vector3D(102.83f, 6.15f, 34.5f);
        JoystickMaxAngleDeg = 14.0f;

        ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-145.6f, 17.475f, 41.0f);
        ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(145.6f, 17.475f, 41.0f);
        TriggerMaxAngleDeg = 16.0f;

        UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
        UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
        UpwardVisibilityRotationPointLeft = new Vector3D(-102.528f, 0.0f, 58.365f);
        UpwardVisibilityRotationPointRight = new Vector3D(102.528f, 0.0f, 58.365f);

        // load model(s)
        BackIcon = modelImporter.Load($"3DModels/{ModelName}/BackIcon.obj");
        VolumeUp = modelImporter.Load($"3DModels/{ModelName}/VolumeUp.obj");
        VolumeDown = modelImporter.Load($"3DModels/{ModelName}/VolumeDown.obj");
        ThreeDots = modelImporter.Load($"3DModels/{ModelName}/ThreeDots.obj");
        MainBodyLeftOver = modelImporter.Load($"3DModels/{ModelName}/MainBodyLeftOver.obj");
        SteamText = modelImporter.Load($"3DModels/{ModelName}/SteamText.obj");
        StartIcon = modelImporter.Load($"3DModels/{ModelName}/StartIcon.obj");
        Screen = modelImporter.Load($"3DModels/{ModelName}/Screen.obj");
        PowerButton = modelImporter.Load($"3DModels/{ModelName}/PowerButton.obj");

        B1Symbol = modelImporter.Load($"3DModels/{ModelName}/B1-Symbol.obj");
        B2Symbol = modelImporter.Load($"3DModels/{ModelName}/B2-Symbol.obj");
        B3Symbol = modelImporter.Load($"3DModels/{ModelName}/B3-Symbol.obj");
        B4Symbol = modelImporter.Load($"3DModels/{ModelName}/B4-Symbol.obj");

        // pull model(s)
        model3DGroup.Children.Add(BackIcon);
        model3DGroup.Children.Add(VolumeUp);
        model3DGroup.Children.Add(VolumeDown);
        model3DGroup.Children.Add(ThreeDots);
        model3DGroup.Children.Add(MainBodyLeftOver);
        model3DGroup.Children.Add(SteamText);
        model3DGroup.Children.Add(StartIcon);
        model3DGroup.Children.Add(Screen);
        model3DGroup.Children.Add(PowerButton);

        model3DGroup.Children.Add(B1Symbol);
        model3DGroup.Children.Add(B2Symbol);
        model3DGroup.Children.Add(B3Symbol);
        model3DGroup.Children.Add(B4Symbol);

        // specific button material(s)
        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
        {
            Material buttonMaterial = null;

            if (ButtonMap.TryGetValue(button, out var map))
                foreach (var model3D in map)
                {
                    switch (button)
                    {
                        case ButtonFlags.B1:
                        case ButtonFlags.B2:
                        case ButtonFlags.B3:
                        case ButtonFlags.B4:
                        case ButtonFlags.DPadUp:
                        case ButtonFlags.DPadRight:
                        case ButtonFlags.DPadDown:
                        case ButtonFlags.DPadLeft:
                            buttonMaterial = MaterialPlasticBlack;
                            break;
                        case ButtonFlags.LeftStickClick:
                        case ButtonFlags.RightStickClick:
                            buttonMaterial = MaterialPlasticWhite;
                            break;

                        case ButtonFlags.LeftStickTouch:
                            buttonMaterial = MaterialPlasticBlack;
                            LeftThumb = model3D;
                            break;
                        case ButtonFlags.RightStickTouch:
                            buttonMaterial = MaterialPlasticBlack;
                            RightThumb = model3D;
                            break;

                        default:
                            buttonMaterial = MaterialPlasticDarkGrey;
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
            ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticDarkGrey;
            ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticDarkGrey;
            DefaultMaterials[model3D] = MaterialPlasticDarkGrey;

            // specific material(s)
            if (model3D.Equals(Screen) || model3D.Equals(LeftThumbRing) || model3D.Equals(RightThumbRing))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                DefaultMaterials[model3D] = MaterialPlasticBlack;
                continue;
            }

            // specific material(s)
            if (model3D.Equals(SteamText) || model3D.Equals(ThreeDots)
                                          || model3D.Equals(BackIcon) || model3D.Equals(StartIcon)
                                          || model3D.Equals(B1Symbol) || model3D.Equals(B2Symbol) ||
                                          model3D.Equals(B3Symbol) || model3D.Equals(B4Symbol)
               )
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                DefaultMaterials[model3D] = MaterialPlasticWhite;
            }
        }

        DrawHighligths();
    }

    private new void DrawHighligths()
    {
        var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];
        var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

        foreach (Model3DGroup model3D in model3DGroup.Children)
            // generic material(s)
            HighlightMaterials[model3D] = MaterialHighlight;
    }
}