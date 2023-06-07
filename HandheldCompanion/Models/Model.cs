using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ControllerCommon.Inputs;
using HelixToolkit.Wpf;
using Application = System.Windows.Application;
using Color = System.Drawing.Color;

namespace HandheldCompanion;

public abstract class IModel
{
    public SortedDictionary<ButtonFlags, List<Model3DGroup>> ButtonMap = new();

    // Materials
    public Dictionary<Model3DGroup, Material> DefaultMaterials = new();
    public Dictionary<Model3DGroup, Material> HighlightMaterials = new();
    public float JoystickMaxAngleDeg;

    // Rotation Points
    public Vector3D JoystickRotationPointCenterLeftMillimeter;
    public Vector3D JoystickRotationPointCenterRightMillimeter;
    public Model3DGroup LeftMotor;
    public Model3DGroup LeftShoulderTrigger;

    // Common groups
    public Model3DGroup LeftThumb;
    public Model3DGroup LeftThumbRing;

    public Model3DGroup MainBody;

    // Model3D vars
    public Model3DGroup model3DGroup = new();
    protected ModelImporter modelImporter = new();

    public string ModelName;
    public Model3DGroup RightMotor;
    public Model3DGroup RightShoulderTrigger;
    public Model3DGroup RightThumb;
    public Model3DGroup RightThumbRing;
    public Vector3D ShoulderTriggerRotationPointCenterLeftMillimeter;
    public Vector3D ShoulderTriggerRotationPointCenterRightMillimeter;
    public float TriggerMaxAngleDeg;
    public Vector3D UpwardVisibilityRotationAxisLeft;
    public Vector3D UpwardVisibilityRotationAxisRight;
    public Vector3D UpwardVisibilityRotationPointLeft;
    public Vector3D UpwardVisibilityRotationPointRight;

    protected IModel(string ModelName)
    {
        this.ModelName = ModelName;

        // load model(s)
        LeftThumbRing = modelImporter.Load($"models/{ModelName}/Joystick-Left-Ring.obj");
        RightThumbRing = modelImporter.Load($"models/{ModelName}/Joystick-Right-Ring.obj");
        LeftMotor = modelImporter.Load($"models/{ModelName}/MotorLeft.obj");
        RightMotor = modelImporter.Load($"models/{ModelName}/MotorRight.obj");
        MainBody = modelImporter.Load($"models/{ModelName}/MainBody.obj");
        LeftShoulderTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Trigger.obj");
        RightShoulderTrigger = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Trigger.obj");

        // map model(s)
        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
        {
            var filename = $"models/{ModelName}/{button}.obj";
            if (File.Exists(filename))
            {
                var model = modelImporter.Load(filename);
                ButtonMap.Add(button, new List<Model3DGroup> { model });

                switch (button)
                {
                    // specific case, being both a button and a trigger
                    case ButtonFlags.LeftThumb:
                        LeftThumb = model;
                        break;
                    case ButtonFlags.RightThumb:
                        RightThumb = model;
                        break;
                }

                // pull model
                model3DGroup.Children.Add(model);
            }
        }

        // pull model(s)
        model3DGroup.Children.Add(LeftThumbRing);
        model3DGroup.Children.Add(RightThumbRing);
        model3DGroup.Children.Add(LeftMotor);
        model3DGroup.Children.Add(RightMotor);
        model3DGroup.Children.Add(MainBody);
        model3DGroup.Children.Add(LeftShoulderTrigger);
        model3DGroup.Children.Add(RightShoulderTrigger);
    }

    protected virtual void DrawHighligths()
    {
        foreach (Model3DGroup model3D in model3DGroup.Children)
        {
            var material = DefaultMaterials[model3D];
            if (material.GetType() != typeof(DiffuseMaterial))
                continue;

            // determine colors from brush from materials
            var DefaultMaterialBrush = ((DiffuseMaterial)material).Brush;
            var StartColor = ((SolidColorBrush)DefaultMaterialBrush).Color;

            // generic material(s)
            var drawingColor =
                ControlPaint.LightLight(Color.FromArgb(StartColor.A, StartColor.R, StartColor.G, StartColor.B));
            var outColor =
                System.Windows.Media.Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
            var solidColor = new SolidColorBrush(outColor);

            HighlightMaterials[model3D] = new DiffuseMaterial(solidColor);
        }
    }

    protected virtual void DrawAccentHighligths()
    {
        var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];
        var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

        foreach (Model3DGroup model3D in model3DGroup.Children)
            // generic material(s)
            HighlightMaterials[model3D] = MaterialHighlight;
    }
}