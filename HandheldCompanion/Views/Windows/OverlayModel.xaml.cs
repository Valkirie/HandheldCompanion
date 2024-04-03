using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using NumQuaternion = System.Numerics.Quaternion;
using NumVector3 = System.Numerics.Vector3;

namespace HandheldCompanion.Views.Windows;

/// <summary>
///     Interaction logic for Overlay.xaml
/// </summary>
public partial class OverlayModel : OverlayWindow
{
    private readonly Timer UpdateTimer;

    private IModel CurrentModel;
    public Vector3D DesiredAngleDeg = new(0, 0, 0);
    private Quaternion DevicePose;
    private Vector3D DevicePoseRad;
    private Vector3D DiffAngle = new(0, 0, 0);

    public bool FaceCamera = false;
    public Vector3D FaceCameraObjectAlignment;

    private ControllerState Inputs = new();

    private OverlayModelMode Modelmode;
    public bool MotionActivated = true;

    private Transform3DGroup Transform3DGroupModelPrevious = new();

    private float ShoulderButtonsAngleDegLeftPrev;
    private float ShoulderTriggerAngleRightPrev;
    private float TriggerAngleShoulderLeft;
    private float ShoulderTriggerAngleLeftPrev;
    private float TriggerAngleShoulderRight;
    private float TriggerAngleShoulderRightPrev;

    public OverlayModel()
    {
        InitializeComponent();
        this._hotkeyId = 1;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        ResetModelPose();

        // initialize timers
        UpdateTimer = new Timer(33);
        UpdateTimer.AutoReset = true;
        UpdateTimer.Elapsed += DrawModel;

        UpdateModel();
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "OverlayControllerMotion":
                    MotionActivated = Convert.ToBoolean(value);
                    
                    // On change of motion activated, reset object alignment
                    ResetModelPose();
                    
                    break;
            }
        });
    }

    private void ResetModelPose()
    {
        // Reset model to initial pose
        FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d);
        DevicePose = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        DevicePoseRad = new Vector3D(0, 3.14, 0);
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        UpdateTimer.Elapsed -= DrawModel;
        UpdateTimer.Stop();
    }

    public void UpdateInterval(double interval)
    {
        UpdateTimer.Interval = interval;
    }

    public void UpdateOverlayMode(OverlayModelMode Modelmode)
    {
        if (this.Modelmode == Modelmode)
            return;

        this.Modelmode = Modelmode;
        UpdateModel();
    }

    public void UpdateModel()
    {
        IModel newModel = null;
        switch (Modelmode)
        {
            case OverlayModelMode.DualSense:
                newModel = new ModelDualSense();
                break;
            case OverlayModelMode.DualShock4:
                newModel = new ModelDS4();
                break;
            case OverlayModelMode.EightBitDoLite2:
                newModel = new Model8BitDoLite2();
                break;
            case OverlayModelMode.N64:
                newModel = new ModelN64();
                break;
            case OverlayModelMode.SteamDeck:
                newModel = new ModelSteamDeck();
                break;
            case OverlayModelMode.Toy:
                newModel = new ModelToyController();
                break;
            default:
            case OverlayModelMode.Xbox360:
                newModel = new ModelXBOX360();
                break;
            case OverlayModelMode.XboxOne:
                newModel = new ModelXBOXOne();
                break;
        }

        if (newModel != CurrentModel)
        {
            CurrentModel = newModel;

            ModelVisual3D.Content = CurrentModel.model3DGroup;
            ModelViewPort.ZoomExtents();
        }
    }

    public override void ToggleVisibility()
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Visible:
                    UpdateTimer.Stop();
                    Hide();
                    break;
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    UpdateTimer.Start();
                    Show();
                    break;
            }
        });
    }

    #region ModelVisual3D

    private RotateTransform3D DeviceRotateTransform;
    private RotateTransform3D DeviceRotateTransformFaceCameraX;
    private RotateTransform3D DeviceRotateTransformFaceCameraY;
    private RotateTransform3D DeviceRotateTransformFaceCameraZ;

    public void UpdateReport(ControllerState Inputs, GamepadMotion gamepadMotion)
    {
        this.Inputs = Inputs;

        // Add return here if motion is not wanted for 3D model
        if (!MotionActivated)
            return;

        // Rotate for different coordinate system of 3D model and motion algorithm
        // Motion algorithm uses DS4 coordinate system
        // 3D model, has Z+ up, X+ to the right, Y+ towards the screen
        gamepadMotion.GetOrientation(out float w, out float x, out float y, out float z);
        DevicePose = new Quaternion(x, y, z, w);

        // Also make euler equivalent availible of quaternions
        NumVector3 euler = InputUtils.ToEulerAngles(DevicePose);
        DevicePoseRad = new Vector3D(-euler.X, -euler.Y, -euler.Z);
    }

    private void DrawModel(object? sender, EventArgs e)
    {
        // Skip if we don't have a model
        if (CurrentModel is null)
            return;

        // Update only if 3D overlay is visible
        if (Visibility != Visibility.Visible)
            return;

        HighLightButtons();

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // Define transformation group for model
            var Transform3DGroupModel = new Transform3DGroup();

            // Device transformation based on pose
            var Ax3DDevicePose = new QuaternionRotation3D(DevicePose);
            DeviceRotateTransform = new RotateTransform3D(Ax3DDevicePose);
            Transform3DGroupModel.Children.Add(DeviceRotateTransform);

            // User requested resting pitch of device around X axis
            var Ax3DRestingPitch = new AxisAngleRotation3D(new Vector3D(1, 0, 0), DesiredAngleDeg.X);
            RotateTransform3D DeviceRotateTransform3DRestingPitch = new RotateTransform3D(Ax3DRestingPitch);
            Transform3DGroupModel.Children.Add(DeviceRotateTransform3DRestingPitch);

            // Rotate imported 3D model around Z axis to align viewport front with front of device
            var Ax3DImportViewPortCorrection = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90);
            RotateTransform3D DeviceRotateTransform3DImportViewPortCorrection = new RotateTransform3D(Ax3DImportViewPortCorrection);
            Transform3DGroupModel.Children.Add(DeviceRotateTransform3DImportViewPortCorrection);

            // Determine diff angles
            DiffAngle.X = InputUtils.rad2deg((float)DevicePoseRad.X) - (float)FaceCameraObjectAlignment.X -
                          (float)DesiredAngleDeg.X;
            DiffAngle.Y = InputUtils.rad2deg((float)DevicePoseRad.Y) - (float)FaceCameraObjectAlignment.Y -
                          (float)DesiredAngleDeg.Y;
            DiffAngle.Z = InputUtils.rad2deg((float)DevicePoseRad.Z) - (float)FaceCameraObjectAlignment.Z -
                          (float)DesiredAngleDeg.Z;

            // Handle wrap around at -180 +180 position which is horizontal for steering
            DiffAngle.Y = (float)DevicePoseRad.Y < 0.0 ? DiffAngle.Y += 180.0f : DiffAngle.Y -= 180.0f;

            // Correction amount for camera, increase slowly
            FaceCameraObjectAlignment += DiffAngle * 0.0015; // 0.0015 = ~90 degrees in 30 seconds

            // Devices rotates (slowly) towards a default position facing the camara 
            // Calculation above is done to:
            // - "quickly" move to the correct pose when enabled as it's calculated in the background
            // - rotate shoulder buttons into view requires angle value

            if (FaceCamera)
            {
                // Transform YZX
                var Ax3DFaceCameraY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), FaceCameraObjectAlignment.Y);
                DeviceRotateTransformFaceCameraY = new RotateTransform3D(Ax3DFaceCameraY);
                Transform3DGroupModel.Children.Add(DeviceRotateTransformFaceCameraY);

                var Ax3DFaceCameraZ = new AxisAngleRotation3D(new Vector3D(0, 0, 1), -FaceCameraObjectAlignment.Z);
                DeviceRotateTransformFaceCameraZ = new RotateTransform3D(Ax3DFaceCameraZ);
                Transform3DGroupModel.Children.Add(DeviceRotateTransformFaceCameraZ);

                var Ax3DFaceCameraX = new AxisAngleRotation3D(new Vector3D(1, 0, 0), FaceCameraObjectAlignment.X);
                DeviceRotateTransformFaceCameraX = new RotateTransform3D(Ax3DFaceCameraX);
                Transform3DGroupModel.Children.Add(DeviceRotateTransformFaceCameraX);
            }

            // Transform mode with group if there are any changes
            if (Transform3DGroupModel != Transform3DGroupModelPrevious)
            {
                ModelVisual3D.Content.Transform = Transform3DGroupModel;
                Transform3DGroupModelPrevious = Transform3DGroupModel;
            }

            // Upward visibility rotation for shoulder buttons
            // Model angle to compensate for

            var ModelPoseXDeg = 0.0f;

            if (FaceCamera && MotionActivated)
                ModelPoseXDeg = InputUtils.rad2deg((float)DevicePoseRad.X) - (float)FaceCameraObjectAlignment.X;
            else
                // Model pose is only adjusted by requested pitch angle
                ModelPoseXDeg = (float)DesiredAngleDeg.X;

            var ShoulderButtonsAngleDeg = 0.0f;

            // Rotate shoulder buttons based on ModelPoseXDeg
            if (ModelPoseXDeg < 0)
                ShoulderButtonsAngleDeg = Math.Clamp(90.0f - 1 * ModelPoseXDeg, 90.0f, 180.0f);
            else if (ModelPoseXDeg >= 0 && ModelPoseXDeg <= 45.0f)
                ShoulderButtonsAngleDeg = 90.0f - 2 * ModelPoseXDeg;
            else if (ModelPoseXDeg < 45.0f) ShoulderButtonsAngleDeg = 0.0f;

            // Update shoulder buttons left, rotate into view angle, trigger angle and trigger gradient color
            UpdateShoulderButtons(
                ref CurrentModel.LeftShoulderTrigger,
                ref TriggerAngleShoulderLeft,
                CurrentModel.TriggerMaxAngleDeg,
                AxisFlags.L2,
                ShoulderButtonsAngleDeg,
                ref ShoulderTriggerAngleLeftPrev,
                ref ShoulderButtonsAngleDegLeftPrev,
                CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger],
                CurrentModel.HighlightMaterials
            );

            // Update shoulder buttons right, rotate into view angle, trigger angle and trigger gradient color
            UpdateShoulderButtons(
                ref CurrentModel.RightShoulderTrigger,
                ref TriggerAngleShoulderRight,
                CurrentModel.TriggerMaxAngleDeg,
                AxisFlags.R2,
                ShoulderButtonsAngleDeg,
                ref TriggerAngleShoulderRightPrev,
                ref ShoulderTriggerAngleRightPrev,
                CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger],
                CurrentModel.HighlightMaterials
            );

            // Update left joystick
            UpdateJoystick(
                AxisFlags.LeftStickX,
                AxisFlags.LeftStickY,
                CurrentModel.LeftThumbRing,
                CurrentModel.LeftThumb,
                CurrentModel.JoystickRotationPointCenterLeftMillimeter,
                CurrentModel.JoystickMaxAngleDeg,
                CurrentModel.DefaultMaterials,
                CurrentModel.HighlightMaterials);

            // Update right joystick
            UpdateJoystick(
                AxisFlags.RightStickX,
                AxisFlags.RightStickY,
                CurrentModel.RightThumbRing,
                CurrentModel.RightThumb,
                CurrentModel.JoystickRotationPointCenterRightMillimeter,
                CurrentModel.JoystickMaxAngleDeg,
                CurrentModel.DefaultMaterials,
                CurrentModel.HighlightMaterials);
        });
    }

    private void HighLightButtons()
    {
        // Color buttons based on pressed state
        foreach (var button in (ButtonFlags[])Enum.GetValues(typeof(ButtonFlags)))
        {
            // Check if the button exists for model, if not, exit foreach
            if (!CurrentModel.ButtonMap.ContainsKey(button))
                continue;

            // Execute the following code on the UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // Todo, there is a bug here when switching 3D overlay type that
                // things are checked from a controller that does not exist or opposite
                // Need to interlock this highlight button thing to not be called when changing controllers?
                // Alternatively, could be an issue with the UI thread still doing it's thing, so stop first?
                // Reproduce by switching several times
                // "System.Collections.Generic.KeyNotFoundException: 'The given key 'Special' was not present in the dictionary.'

                // Iterate over each 3D model associated with the current button
                foreach (Model3DGroup model3DGroup in CurrentModel.ButtonMap[button])
                {
                    GeometryModel3D model3D = (GeometryModel3D)model3DGroup.Children.FirstOrDefault();

                    // Skip if there is no 3D model or if we are dealing with non diffuse material
                    if (model3D is null || model3D.Material is not DiffuseMaterial)
                        continue;

                    // Update the model material based on the button state
                    // If the button is pressed, use the highlight material;
                    // otherwise, use the default material
                    model3D.Material = Inputs.ButtonState[button]
                        ? model3D.BackMaterial = CurrentModel.HighlightMaterials[model3DGroup]
                        : model3D.BackMaterial = CurrentModel.DefaultMaterials[model3DGroup];
                }
            });
        }
    }

    private Material GradientHighlight(Material defaultMaterial, Material highlightMaterial, float factor)
    {
        // Extract start and end colors
        Color startColor = ((SolidColorBrush)((DiffuseMaterial)defaultMaterial).Brush).Color;
        Color endColor = ((SolidColorBrush)((DiffuseMaterial)highlightMaterial).Brush).Color;

        // Interpolate colors
        Color transitionColor = InterpolateColor(startColor, endColor, factor);

        // Create a new SolidColorBrush with the interpolated color
        SolidColorBrush transitionBrush = new SolidColorBrush(transitionColor);

        // Return a new DiffuseMaterial with the interpolated color
        return new DiffuseMaterial(transitionBrush);
    }

    private Color InterpolateColor(Color startColor, Color endColor, float factor)
    {
        byte a = (byte)(startColor.A * (1 - factor) + endColor.A * factor);
        byte r = (byte)(startColor.R * (1 - factor) + endColor.R * factor);
        byte g = (byte)(startColor.G * (1 - factor) + endColor.G * factor);
        byte b = (byte)(startColor.B * (1 - factor) + endColor.B * factor);

        return Color.FromArgb(a, r, g, b);
    }

    private void UpdateJoystick(
    AxisFlags stickX,
    AxisFlags stickY,
    Model3DGroup thumbRing,
    Model3D thumb,
    Vector3D rotationPointCenter,
    float maxAngleDeg,
    Dictionary<Model3DGroup, Material> defaultMaterials,
    Dictionary<Model3DGroup, Material> highlightMaterials)
    {
        GeometryModel3D geometryModel3D = thumbRing.Children[0] as GeometryModel3D;
        // Determine if stick has moved
        bool isStickMoved = Inputs.AxisState[stickX] != 0.0f || Inputs.AxisState[stickY] != 0.0f;

        // Adjust material gradually based on distance from center
        if (isStickMoved)
        {
            float gradientFactor = Math.Max(
                Math.Abs(1 * Inputs.AxisState[stickX] / (float)short.MaxValue),
                Math.Abs(1 * Inputs.AxisState[stickY] / (float)short.MaxValue));

            geometryModel3D.Material = GradientHighlight(
                defaultMaterials[thumbRing],
                highlightMaterials[thumbRing],
                gradientFactor);
        }
        else
        {
            geometryModel3D.Material = defaultMaterials[thumbRing];
        }

        // Define and compute rotation angles
        float x = maxAngleDeg * Inputs.AxisState[stickX] / short.MaxValue;
        float y = -1 * maxAngleDeg * Inputs.AxisState[stickY] / short.MaxValue;

        // Create rotation transformations
        RotateTransform3D rotationX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), x), new Point3D(rotationPointCenter.X, rotationPointCenter.Y, rotationPointCenter.Z));
        RotateTransform3D rotationY = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), y), new Point3D(rotationPointCenter.X, rotationPointCenter.Y, rotationPointCenter.Z));

        // Create Transform3DGroup and add rotations
        Transform3DGroup transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(rotationX);
        transformGroup.Children.Add(rotationY);

        // Apply transformation
        thumbRing.Transform = thumb.Transform = transformGroup;
    }

    private void UpwardVisibilityRotationShoulderButtons(float ShoulderButtonsAngleDeg,
        Vector3D UpwardVisibilityRotationAxis,
        Vector3D UpwardVisibilityRotationPoint,
        float ShoulderTriggerAngleDeg,
        Vector3D ShoulderTriggerRotationPoint,
        ref Model3DGroup ShoulderTrigger,
        ref Model3DGroup ShoulderButton
        )
    {
        // Define rotation group for trigger button to combine rotations
        var Transform3DGroupShoulderTrigger = new Transform3DGroup();

        // Upward visibility rotation vector and angle
        var ax3d = new AxisAngleRotation3D(UpwardVisibilityRotationAxis, ShoulderButtonsAngleDeg);
        var TransformShoulder = new RotateTransform3D(ax3d);

        // Define rotation point shoulder buttons
        TransformShoulder.CenterX = UpwardVisibilityRotationPoint.X;
        TransformShoulder.CenterY = UpwardVisibilityRotationPoint.Y;
        TransformShoulder.CenterZ = UpwardVisibilityRotationPoint.Z;

        // Trigger vector and angle
        ax3d = new AxisAngleRotation3D(UpwardVisibilityRotationAxis, ShoulderTriggerAngleDeg);
        var TransformTriggerPosition = new RotateTransform3D(ax3d);

        // Define rotation point trigger
        TransformTriggerPosition.CenterX = ShoulderTriggerRotationPoint.X;
        TransformTriggerPosition.CenterY = ShoulderTriggerRotationPoint.Y;
        TransformTriggerPosition.CenterZ = ShoulderTriggerRotationPoint.Z;

        // Transform trigger
        // Trigger first, then visibility transform
        Transform3DGroupShoulderTrigger.Children.Add(TransformTriggerPosition);
        Transform3DGroupShoulderTrigger.Children.Add(TransformShoulder);

        // Transform trigger with both upward visibility and trigger position
        ShoulderTrigger.Transform = Transform3DGroupShoulderTrigger;
        // Transform shoulder button only with upward visibility
        ShoulderButton.Transform = TransformShoulder;
    }

    // Shoulder buttons, rotate into view angle, trigger angle and trigger gradient color
    private void UpdateShoulderButtons(
        ref Model3DGroup shoulderTriggerModel,
        ref float triggerAngle,
        float triggerMaxAngleDeg,
        AxisFlags axisFlag,
        float shoulderButtonsAngleDeg,
        ref float triggerAnglePrev,
        ref float shoulderButtonsAngleDegPrev,
        Material defaultMaterial,
        Dictionary<Model3DGroup, Material> highlightMaterials)
    {
        var geometryModel3D = shoulderTriggerModel.Children[0] as GeometryModel3D;

        // Adjust trigger color gradient based on amount of pull
        if (Inputs.AxisState[axisFlag] > 0)
        {
            float gradientFactor = 1 * Inputs.AxisState[axisFlag] / (float)byte.MaxValue;
            geometryModel3D.Material = GradientHighlight(defaultMaterial, highlightMaterials[shoulderTriggerModel], gradientFactor);
        }
        else
        {
            geometryModel3D.Material = defaultMaterial;
        }

        // Determine trigger angle pull in degree
        triggerAngle = -1 * triggerMaxAngleDeg * Inputs.AxisState[axisFlag] / byte.MaxValue;

        // In case device pose changes leading to different visibility angle or if trigger angle changes
        if (shoulderButtonsAngleDeg != shoulderButtonsAngleDegPrev || triggerAngle != triggerAnglePrev)
        {
            Model3DGroup Placeholder = CurrentModel.ButtonMap[axisFlag == AxisFlags.L2 ? ButtonFlags.L1 : ButtonFlags.R1][0];

            UpwardVisibilityRotationShoulderButtons(shoulderButtonsAngleDeg,
                axisFlag == AxisFlags.L2 ? CurrentModel.UpwardVisibilityRotationAxisLeft : CurrentModel.UpwardVisibilityRotationAxisRight,
                axisFlag == AxisFlags.L2 ? CurrentModel.UpwardVisibilityRotationPointLeft : CurrentModel.UpwardVisibilityRotationPointRight,
                triggerAngle,
                axisFlag == AxisFlags.L2 ? CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter : CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter,
                ref shoulderTriggerModel,
                ref Placeholder
            );

            CurrentModel.ButtonMap[axisFlag == AxisFlags.L2 ? ButtonFlags.L1 : ButtonFlags.R1][0] = Placeholder;

            triggerAnglePrev = triggerAngle;
            shoulderButtonsAngleDegPrev = shoulderButtonsAngleDeg;
        }

    }

    #endregion
}