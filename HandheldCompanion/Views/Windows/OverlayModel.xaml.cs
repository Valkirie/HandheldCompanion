using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using System;
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
    private Quaternion DevicePose = new(0.0f, 0.0f, 1.0f, 0.0f);
    private Vector3D DevicePoseRad = new(0, 3.14, 0);
    private Vector3D DiffAngle = new(0, 0, 0);

    public bool FaceCamera = false;
    public Vector3D FaceCameraObjectAlignment = new(0.0d, 0.0d, 0.0d);
    private HIDmode HIDmode;

    private ControllerState Inputs = new();

    private OverlayModelMode Modelmode;
    public bool MotionActivated = true;

    // TODO Dummy variables, placeholder and for testing 
    private float ShoulderButtonsAngleDegPrev;
    private Transform3DGroup Transform3DGroupModelPrevious = new();

    private float TriggerAngleShoulderLeft;
    private float TriggerAngleShoulderLeftPrev;
    private float TriggerAngleShoulderRight;
    private float TriggerAngleShoulderRightPrev;

    public OverlayModel()
    {
        InitializeComponent();
        this._hotkeyId = 1;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        MotionManager.OverlayModelUpdate += MotionManager_OverlayModelUpdate;
        VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

        // initialize timers
        UpdateTimer = new Timer(33);
        UpdateTimer.AutoReset = true;
        UpdateTimer.Elapsed += DrawModel;

        UpdateModel();
    }

    private void VirtualManager_ControllerSelected(HIDmode mode)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            UpdateHIDMode(mode);
        });
    }

    private void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "OverlayControllerMotion":
                    MotionActivated = Convert.ToBoolean(value);

                    // On change of motion activated, reset object alignment
                    FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d);
                    DevicePose = new Quaternion(0.0f, 0.0f, 1.0f, 0.0f);
                    DevicePoseRad = new Vector3D(0, 3.14, 0);
                    break;
            }
        });
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

    public void UpdateHIDMode(HIDmode HIDmode)
    {
        if (this.HIDmode == HIDmode)
            return;

        this.HIDmode = HIDmode;
        UpdateModel();
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
            default:
            case OverlayModelMode.OEM:
                {
                    switch (MainWindow.CurrentDevice.ProductModel)
                    {
                        case "AYANEO2021":
                            newModel = new ModelAYANEO2021();
                            break;
                        case "AYANEONext":
                            newModel = new ModelAYANEONext();
                            break;
                        case "AYANEOAir":
                            newModel = new ModelAYANEOAir();
                            break;
                        case "ONEXPLAYERMini":
                            newModel = new ModelOneXPlayerMini();
                            break;
                        case "SteamDeck":
                            newModel = new ModelSteamDeck();
                            break;
                        default:
                            // default model if unsupported OEM
                            newModel = new ModelXBOX360();
                            break;
                    }
                }
                break;
            case OverlayModelMode.Virtual:
                {
                    switch (HIDmode)
                    {
                        default:
                        case HIDmode.DualShock4Controller:
                            newModel = new ModelDS4();
                            break;
                        case HIDmode.Xbox360Controller:
                            newModel = new ModelXBOX360();
                            break;
                    }
                }
                break;
            case OverlayModelMode.XboxOne:
                {
                    newModel = new ModelXBOXOne();
                }
                break;
            case OverlayModelMode.ZDOPlus:
                {
                    newModel = new ModelZDOPlus();
                }
                break;
            case OverlayModelMode.EightBitDoLite2:
                {
                    newModel = new Model8BitDoLite2();
                }
                break;
            case OverlayModelMode.MachenikeHG510:
                {
                    newModel = new ModelMachenikeHG510();
                }
                break;
            case OverlayModelMode.Toy:
                {
                    newModel = new ModelToyController();
                }
                break;
            case OverlayModelMode.N64:
                {
                    newModel = new ModelN64();
                }
                break;
            case OverlayModelMode.DualSense:
                {
                    newModel = new ModelDualSense();
                }
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
    private RotateTransform3D LeftJoystickRotateTransform;
    private RotateTransform3D RightJoystickRotateTransform;

    private void MotionManager_OverlayModelUpdate(NumVector3 euler, NumQuaternion quaternion)
    {
        // Add return here if motion is not wanted for 3D model
        if (!MotionActivated)
            return;

        // TODO: why is the quaternion order shifted?
        DevicePose = new Quaternion(quaternion.W, quaternion.X, quaternion.Y, quaternion.Z);
        DevicePoseRad = new Vector3D(euler.X, euler.Y, euler.Z);
    }

    private void DrawModel(object? sender, EventArgs e)
    {
        if (CurrentModel is null)
            return;

        // skip virtual controller update if hidden or collapsed
        if (Visibility != Visibility.Visible)
            return;

        Parallel.ForEach((ButtonFlags[])Enum.GetValues(typeof(ButtonFlags)),
            new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, button =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (!CurrentModel.ButtonMap.ContainsKey(button))
                        return;

                    foreach (Model3DGroup model3DGroup in CurrentModel.ButtonMap[button])
                    {
                        GeometryModel3D model3D = (GeometryModel3D)model3DGroup.Children.FirstOrDefault();
                        if (model3D is null || model3D.Material is not DiffuseMaterial)
                            continue;

                        model3D.Material = Inputs.ButtonState[button]
                            ? model3D.BackMaterial = CurrentModel.HighlightMaterials[model3DGroup]
                            : model3D.BackMaterial = CurrentModel.DefaultMaterials[model3DGroup];
                    }
                });
            });

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // update model
            var Transform3DGroupModel = new Transform3DGroup();

            // Device transformation based on pose
            var Ax3DDevicePose = new QuaternionRotation3D(DevicePose);
            DeviceRotateTransform = new RotateTransform3D(Ax3DDevicePose);
            Transform3DGroupModel.Children.Add(DeviceRotateTransform);

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

            if (FaceCamera)
                ModelPoseXDeg = InputUtils.rad2deg((float)DevicePoseRad.X) - (float)FaceCameraObjectAlignment.X;
            else
                // Not slowly rotate into view when face camera is off
                ModelPoseXDeg = InputUtils.rad2deg((float)DevicePoseRad.X);

            var ShoulderButtonsAngleDeg = 0.0f;

            // Rotate shoulder 90 degrees upward while controller faces user
            if (ModelPoseXDeg < 0)
                ShoulderButtonsAngleDeg = Math.Clamp(90.0f - 1 * ModelPoseXDeg, 90.0f, 180.0f);
            // In between rotate inverted from pose
            else if (ModelPoseXDeg >= 0 && ModelPoseXDeg <= 45.0f)
                ShoulderButtonsAngleDeg = 90.0f - 2 * ModelPoseXDeg;
            // Rotate shoulder buttons to original spot at -45 and beyond
            else if (ModelPoseXDeg < 45.0f) ShoulderButtonsAngleDeg = 0.0f;
            var Placeholder = new Model3DGroup();

            // Left shoulder buttons visibility rotation and trigger button angle 
            // only perform when model or triggers are in a different position 
            if (ShoulderButtonsAngleDeg != ShoulderButtonsAngleDegPrev ||
                TriggerAngleShoulderLeft != TriggerAngleShoulderLeftPrev)
            {
                Placeholder = CurrentModel.ButtonMap[ButtonFlags.L1][0];

                UpwardVisibilityRotationShoulderButtons(ShoulderButtonsAngleDeg,
                    CurrentModel.UpwardVisibilityRotationAxisLeft,
                    CurrentModel.UpwardVisibilityRotationPointLeft,
                    TriggerAngleShoulderLeft,
                    CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter,
                    ref CurrentModel.LeftShoulderTrigger,
                    ref Placeholder
                );

                CurrentModel.ButtonMap[ButtonFlags.L1][0] = Placeholder;

                TriggerAngleShoulderLeftPrev = TriggerAngleShoulderLeft;
            }

            // Right shoulder buttons visibility rotation and trigger button angle 
            // only perform when model or triggers are in a different position 
            if (ShoulderButtonsAngleDeg != ShoulderButtonsAngleDegPrev ||
                TriggerAngleShoulderRight != TriggerAngleShoulderRightPrev)
            {
                Placeholder = CurrentModel.ButtonMap[ButtonFlags.R1][0];

                UpwardVisibilityRotationShoulderButtons(ShoulderButtonsAngleDeg,
                    CurrentModel.UpwardVisibilityRotationAxisRight,
                    CurrentModel.UpwardVisibilityRotationPointRight,
                    TriggerAngleShoulderRight,
                    CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter,
                    ref CurrentModel.RightShoulderTrigger,
                    ref Placeholder
                );

                CurrentModel.ButtonMap[ButtonFlags.R1][0] = Placeholder;

                TriggerAngleShoulderRightPrev = TriggerAngleShoulderRight;
            }

            ShoulderButtonsAngleDegPrev = ShoulderButtonsAngleDeg;

            float GradientFactor; // Used for multiple models

            // ShoulderLeftTrigger
            var geometryModel3D = CurrentModel.LeftShoulderTrigger.Children[0] as GeometryModel3D;
            if (Inputs.AxisState[AxisFlags.L2] > 0)
            {
                GradientFactor = 1 * Inputs.AxisState[AxisFlags.L2] / (float)byte.MaxValue;
                geometryModel3D.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger],
                    CurrentModel.HighlightMaterials[CurrentModel.LeftShoulderTrigger],
                    GradientFactor);
            }
            else
            {
                geometryModel3D.Material = CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger];
            }

            TriggerAngleShoulderLeft =
                -1 * CurrentModel.TriggerMaxAngleDeg * Inputs.AxisState[AxisFlags.L2] / byte.MaxValue;

            // ShoulderRightTrigger
            geometryModel3D = CurrentModel.RightShoulderTrigger.Children[0] as GeometryModel3D;

            if (Inputs.AxisState[AxisFlags.R2] > 0)
            {
                GradientFactor = 1 * Inputs.AxisState[AxisFlags.R2] / (float)byte.MaxValue;
                geometryModel3D.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger],
                    CurrentModel.HighlightMaterials[CurrentModel.RightShoulderTrigger],
                    GradientFactor);
            }
            else
            {
                geometryModel3D.Material = CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger];
            }

            TriggerAngleShoulderRight =
                -1 * CurrentModel.TriggerMaxAngleDeg * Inputs.AxisState[AxisFlags.R2] / byte.MaxValue;

            // JoystickLeftRing
            geometryModel3D = CurrentModel.LeftThumbRing.Children[0] as GeometryModel3D;
            if (Inputs.AxisState[AxisFlags.LeftStickX] != 0.0f || Inputs.AxisState[AxisFlags.LeftStickY] != 0.0f)
            {
                // Adjust color
                GradientFactor = Math.Max(Math.Abs(1 * Inputs.AxisState[AxisFlags.LeftStickX] / (float)short.MaxValue),
                    Math.Abs(1 * Inputs.AxisState[AxisFlags.LeftStickY] / (float)short.MaxValue));

                geometryModel3D.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.LeftThumbRing],
                    CurrentModel.HighlightMaterials[CurrentModel.LeftThumbRing],
                    GradientFactor);

                // Define and compute
                var Transform3DGroupJoystickLeft = new Transform3DGroup();
                var x = CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.LeftStickX] / short.MaxValue;
                var y = -1 * CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.LeftStickY] / short.MaxValue;

                // Rotation X
                var ax3d = new AxisAngleRotation3D(new Vector3D(0, 0, 1), x);
                LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                LeftJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterLeftMillimeter.X;
                LeftJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Y;
                LeftJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Z;

                Transform3DGroupJoystickLeft.Children.Add(LeftJoystickRotateTransform);

                // Rotation Y
                ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), y);
                LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                LeftJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterLeftMillimeter.X;
                LeftJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Y;
                LeftJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Z;

                Transform3DGroupJoystickLeft.Children.Add(LeftJoystickRotateTransform);

                // Transform joystick group
                CurrentModel.LeftThumbRing.Transform = CurrentModel.LeftThumb.Transform = Transform3DGroupJoystickLeft;
            }
            else
            {
                // Default material color, no highlight
                geometryModel3D.Material = CurrentModel.DefaultMaterials[CurrentModel.LeftThumbRing];

                // Define and compute, back to default position
                var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                LeftJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterLeftMillimeter.X;
                LeftJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Y;
                LeftJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterLeftMillimeter.Z;

                // Transform joystick
                CurrentModel.LeftThumbRing.Transform = CurrentModel.LeftThumb.Transform = LeftJoystickRotateTransform;
            }

            // JoystickRightRing
            geometryModel3D = CurrentModel.RightThumbRing.Children[0] as GeometryModel3D;
            if (Inputs.AxisState[AxisFlags.RightStickX] != 0.0f || Inputs.AxisState[AxisFlags.RightStickY] != 0.0f)
            {
                // Adjust color
                GradientFactor = Math.Max(Math.Abs(1 * Inputs.AxisState[AxisFlags.RightStickX] / (float)short.MaxValue),
                    Math.Abs(1 * Inputs.AxisState[AxisFlags.RightStickY] / (float)short.MaxValue));

                geometryModel3D.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.RightThumbRing],
                    CurrentModel.HighlightMaterials[CurrentModel.RightThumbRing],
                    GradientFactor);

                // Define and compute
                var Transform3DGroupJoystickRight = new Transform3DGroup();
                var x = CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.RightStickX] / short.MaxValue;
                var y = -1 * CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.RightStickY] /
                        short.MaxValue;

                // Rotation X
                var ax3d = new AxisAngleRotation3D(new Vector3D(0, 0, 1), x);
                RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                RightJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterRightMillimeter.X;
                RightJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterRightMillimeter.Y;
                RightJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterRightMillimeter.Z;

                Transform3DGroupJoystickRight.Children.Add(RightJoystickRotateTransform);

                // Rotation Y
                ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), y);
                RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                RightJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterRightMillimeter.X;
                RightJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterRightMillimeter.Y;
                RightJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterRightMillimeter.Z;

                Transform3DGroupJoystickRight.Children.Add(RightJoystickRotateTransform);

                // Transform joystick group
                CurrentModel.RightThumbRing.Transform =
                    CurrentModel.RightThumb.Transform = Transform3DGroupJoystickRight;
            }
            else
            {
                geometryModel3D.Material = CurrentModel.DefaultMaterials[CurrentModel.RightThumbRing];

                // Define and compute, back to default position
                var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                // Define rotation point
                RightJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterRightMillimeter.X;
                RightJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterRightMillimeter.Y;
                RightJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterRightMillimeter.Z;

                // Transform joystick
                CurrentModel.RightThumbRing.Transform =
                    CurrentModel.RightThumb.Transform = RightJoystickRotateTransform;
            }
        });
    }

    public void UpdateReport(ControllerState Inputs)
    {
        this.Inputs = Inputs;
    }

    private Material GradientHighlight(Material DefaultMaterial, Material HighlightMaterial, float Factor)
    {
        // Determine colors from brush from materials
        var DefaultMaterialBrush = ((DiffuseMaterial)DefaultMaterial).Brush;
        var StartColor = ((SolidColorBrush)DefaultMaterialBrush).Color;
        var HighlightMaterialBrush = ((DiffuseMaterial)HighlightMaterial).Brush;
        var EndColor = ((SolidColorBrush)HighlightMaterialBrush).Color;

        // Linear interpolate color
        var bk = 1 - Factor;
        var a = StartColor.A * bk + EndColor.A * Factor;
        var r = StartColor.R * bk + EndColor.R * Factor;
        var g = StartColor.G * bk + EndColor.G * Factor;
        var b = StartColor.B * bk + EndColor.B * Factor;

        // Define color
        var TransitionColor = Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);

        // Return material with transition color
        return new DiffuseMaterial(new SolidColorBrush(TransitionColor));
    }

    private void UpwardVisibilityRotationShoulderButtons(float ShoulderButtonsAngleDeg,
        Vector3D UpwardVisibilityRotationAxis,
        Vector3D UpwardVisibilityRotationPoint,
        float ShoulderTriggerAngleDeg,
        Vector3D ShoulderTriggerRotationPointCenterMillimeter,
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
        TransformTriggerPosition.CenterX = ShoulderTriggerRotationPointCenterMillimeter.X;
        TransformTriggerPosition.CenterY = ShoulderTriggerRotationPointCenterMillimeter.Y;
        TransformTriggerPosition.CenterZ = ShoulderTriggerRotationPointCenterMillimeter.Z;

        // Transform trigger
        // Trigger first, then visibility transform
        Transform3DGroupShoulderTrigger.Children.Add(TransformTriggerPosition);
        Transform3DGroupShoulderTrigger.Children.Add(TransformShoulder);

        // Transform trigger with both upward visibility and trigger position
        ShoulderTrigger.Transform = Transform3DGroupShoulderTrigger;
        // Transform shoulder button only with upward visibility
        ShoulderButton.Transform = TransformShoulder;
    }

    #endregion
}