using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models;

internal class ModelZDOPlus : IModel
{
    private readonly Model3DGroup FunctionButton1;
    private readonly Model3DGroup FunctionButton2;
    private readonly Model3DGroup FunctionButton3;
    private readonly Model3DGroup FunctionButton4;
    private readonly Model3DGroup HomeButton;
    private readonly Model3DGroup JoystickLeftCover;
    private readonly Model3DGroup JoystickLeftLEDRing;
    private readonly Model3DGroup JoystickRightCover;
    private readonly Model3DGroup JoystickRightLEDRing;
    private readonly Model3DGroup LED1;
    private readonly Model3DGroup LED2;
    private readonly Model3DGroup LED3;
    private readonly Model3DGroup LED4;
    private readonly Model3DGroup LeftK;

    private readonly Model3DGroup Logo;

    // Specific groups (move me)
    private readonly Model3DGroup MainBodyRight;
    private readonly Model3DGroup MainBodyRubberLeft;
    private readonly Model3DGroup MainBodyRubberRight;
    private readonly Model3DGroup Paddle1;
    private readonly Model3DGroup Paddle2;
    private readonly Model3DGroup Paddle3;
    private readonly Model3DGroup Paddle4;
    private readonly Model3DGroup Paddle5;
    private readonly Model3DGroup Paddle6;
    private readonly Model3DGroup RightK;

    public ModelZDOPlus() : base("ZDOPlus")
    {
        // colors
        var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#707477");
        var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#D6E7EE");
        var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];

        var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
        var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
        var MaterialPlasticTransparentHighlight = new SpecularMaterial(ColorHighlight, 0.3);
        var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

        // Rotation Points
        JoystickRotationPointCenterLeftMillimeter = new Vector3D(-39.0f, -1.2f, 16.5f);
        JoystickRotationPointCenterRightMillimeter = new Vector3D(20f, -1.2f, -7f);
        JoystickMaxAngleDeg = 14.0f;

        ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-31.87f, 2.67f, 41.38);
        ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(31.87f, 2.67f, 41.38);
        TriggerMaxAngleDeg = 16.0f;

        UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
        UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
        UpwardVisibilityRotationPointLeft = new Vector3D(-36.226f, -14.26f, 47.332f);
        UpwardVisibilityRotationPointRight = new Vector3D(36.226f, -14.26f, 47.332f);

        // load model(s)
        MainBodyRight = modelImporter.Load($"models/{ModelName}/MainBodyRight.obj");
        MainBodyRubberLeft = modelImporter.Load($"models/{ModelName}/MainBodyRubberLeft.obj");
        MainBodyRubberRight = modelImporter.Load($"models/{ModelName}/MainBodyRubberRight.obj");
        FunctionButton1 = modelImporter.Load($"models/{ModelName}/FunctionButton1.obj");
        FunctionButton2 = modelImporter.Load($"models/{ModelName}/FunctionButton2.obj");
        FunctionButton3 = modelImporter.Load($"models/{ModelName}/FunctionButton3.obj");
        FunctionButton4 = modelImporter.Load($"models/{ModelName}/FunctionButton4.obj");
        Logo = modelImporter.Load($"models/{ModelName}/Logo.obj");
        HomeButton = modelImporter.Load($"models/{ModelName}/HomeButton.obj");
        LED1 = modelImporter.Load($"models/{ModelName}/LED1.obj");
        LED2 = modelImporter.Load($"models/{ModelName}/LED2.obj");
        LED3 = modelImporter.Load($"models/{ModelName}/LED3.obj");
        LED4 = modelImporter.Load($"models/{ModelName}/LED4.obj");
        LeftK = modelImporter.Load($"models/{ModelName}/LeftK.obj");
        RightK = modelImporter.Load($"models/{ModelName}/RightK.obj");
        Paddle1 = modelImporter.Load($"models/{ModelName}/Paddle1.obj");
        Paddle2 = modelImporter.Load($"models/{ModelName}/Paddle2.obj");
        Paddle3 = modelImporter.Load($"models/{ModelName}/Paddle3.obj");
        Paddle4 = modelImporter.Load($"models/{ModelName}/Paddle4.obj");
        Paddle5 = modelImporter.Load($"models/{ModelName}/Paddle5.obj");
        Paddle6 = modelImporter.Load($"models/{ModelName}/Paddle6.obj");
        JoystickLeftLEDRing = modelImporter.Load($"models/{ModelName}/JoystickLeftLEDRing.obj");
        JoystickRightLEDRing = modelImporter.Load($"models/{ModelName}/JoystickRightLEDRing.obj");
        JoystickLeftCover = modelImporter.Load($"models/{ModelName}/JoystickLeftCover.obj");
        JoystickRightCover = modelImporter.Load($"models/{ModelName}/JoystickRightCover.obj");

        // pull model(s)
        model3DGroup.Children.Add(MainBodyRight);
        model3DGroup.Children.Add(MainBodyRubberRight);
        model3DGroup.Children.Add(MainBodyRubberLeft);
        model3DGroup.Children.Add(FunctionButton1);
        model3DGroup.Children.Add(FunctionButton2);
        model3DGroup.Children.Add(FunctionButton3);
        model3DGroup.Children.Add(FunctionButton4);
        model3DGroup.Children.Add(Logo);
        model3DGroup.Children.Add(HomeButton);
        model3DGroup.Children.Add(LED1);
        model3DGroup.Children.Add(LED2);
        model3DGroup.Children.Add(LED3);
        model3DGroup.Children.Add(LED4);
        model3DGroup.Children.Add(LeftK);
        model3DGroup.Children.Add(RightK);
        model3DGroup.Children.Add(Paddle1);
        model3DGroup.Children.Add(Paddle2);
        model3DGroup.Children.Add(Paddle3);
        model3DGroup.Children.Add(Paddle4);
        model3DGroup.Children.Add(Paddle5);
        model3DGroup.Children.Add(Paddle6);
        model3DGroup.Children.Add(JoystickLeftLEDRing);
        model3DGroup.Children.Add(JoystickRightLEDRing);
        model3DGroup.Children.Add(JoystickLeftCover);
        model3DGroup.Children.Add(JoystickRightCover);

        foreach (Model3DGroup model3D in model3DGroup.Children)
        {
            if (DefaultMaterials.ContainsKey(model3D))
                continue;

            // generic material(s)
            ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
            ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack; // ZDO is not solid closed
            DefaultMaterials[model3D] = MaterialPlasticBlack;

            // specific material(s)
            if (model3D.Equals(MainBody) || model3D.Equals(MainBodyRight) || model3D.Equals(LeftMotor) ||
                model3D.Equals(RightMotor))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                DefaultMaterials[model3D] = MaterialPlasticWhite;
                continue;
            }

            if (model3D.Equals(Logo))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialHighlight;
                DefaultMaterials[model3D] = MaterialHighlight;
                continue;
            }

            if (model3D.Equals(HomeButton) || model3D.Equals(LED1) || model3D.Equals(JoystickLeftLEDRing) ||
                model3D.Equals(JoystickRightLEDRing))
            {
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticTransparentHighlight;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticTransparentHighlight;
                DefaultMaterials[model3D] = MaterialPlasticTransparentHighlight;
            }
        }

        base.DrawAccentHighligths();
    }
}