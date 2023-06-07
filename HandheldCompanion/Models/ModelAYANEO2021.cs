using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models;

internal class ModelAYANEO2021 : IModel
{
    private readonly Model3DGroup JoystickLeftCover;
    private readonly Model3DGroup JoystickRightCover;
    private readonly Model3DGroup LeftShoulderMiddle;
    private readonly Model3DGroup RightShoulderMiddle;

    private readonly Model3DGroup Screen;

    // Specific groups (move me)
    private readonly Model3DGroup WFBH;
    private readonly Model3DGroup WFBRGB;
    private readonly Model3DGroup WFBTM;

    public ModelAYANEO2021() : base("AYANEO 2021")
    {
        // colors
        var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
        var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");

        var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
        var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

        // Rotation Points
        JoystickRotationPointCenterLeftMillimeter = new Vector3D(-109.0f, -8.0f, 23.0f);
        JoystickRotationPointCenterRightMillimeter = new Vector3D(104.0f, -8.0f, -6.0f);
        JoystickMaxAngleDeg = 19.0f;

        ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-105.951f, 1.25f, 46.814f);
        ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(105.951f, 1.25f, 46.814f);
        TriggerMaxAngleDeg = 16.0f;

        UpwardVisibilityRotationAxisLeft = new Vector3D(26.915, 0, 7.27);
        UpwardVisibilityRotationAxisRight = new Vector3D(26.915, 0, -7.27);
        UpwardVisibilityRotationPointLeft = new Vector3D(-93.32f, -10.5f, 54.05f);
        UpwardVisibilityRotationPointRight = new Vector3D(93.32, -10.5f, 54.05f);

        // load model(s)
        WFBH = modelImporter.Load($"models/{ModelName}/WFB-H.obj");
        WFBRGB = modelImporter.Load($"models/{ModelName}/WFB-RGB.obj");
        WFBTM = modelImporter.Load($"models/{ModelName}/WFB-TM.obj");
        LeftShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
        RightShoulderMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");
        Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
        JoystickLeftCover = modelImporter.Load($"models/{ModelName}/Joystick-Left-Cover.obj");
        JoystickRightCover = modelImporter.Load($"models/{ModelName}/Joystick-Right-Cover.obj");

        // pull model(s)
        model3DGroup.Children.Add(WFBH);
        model3DGroup.Children.Add(WFBRGB);
        model3DGroup.Children.Add(WFBTM);
        model3DGroup.Children.Add(LeftShoulderMiddle);
        model3DGroup.Children.Add(RightShoulderMiddle);
        model3DGroup.Children.Add(JoystickLeftCover);
        model3DGroup.Children.Add(JoystickRightCover);
        model3DGroup.Children.Add(Screen);

        foreach (Model3DGroup model3D in model3DGroup.Children)
        {
            if (DefaultMaterials.ContainsKey(model3D))
                continue;

            // specific material(s)
            if (model3D.Equals(MainBody) || model3D.Equals(LeftMotor) || model3D.Equals(RightMotor))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                DefaultMaterials[model3D] = MaterialPlasticWhite;
                continue;
            }

            // generic material(s)
            ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
            DefaultMaterials[model3D] = MaterialPlasticBlack;
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