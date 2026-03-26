using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using NumQuaternion = System.Numerics.Quaternion;
using NumVector3 = System.Numerics.Vector3;

namespace HandheldCompanion.Views.Windows;

/// <summary>
///     Interaction logic for Overlay.xaml
/// </summary>
public partial class OverlayModel : OverlayWindow
{
    private const float ShortMaxValueInverse = 1.0f / short.MaxValue;
    private const float ByteMaxValueInverse = 1.0f / byte.MaxValue;
    private readonly float _faceCameraAlignmentRatePerSecond = 0.18f;

    private readonly Timer UpdateTimer;

    private IModel CurrentModel;
    public Vector3D DesiredAngleDeg = new(0, 0, 0);
    private Quaternion DevicePose;
    private Vector3D DevicePoseDeg;
    private Vector3D DiffAngle = new(0, 0, 0);

    private static readonly ButtonFlags[] resetFlags = [ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4];

    // settings
    private float RestingPitchAngleDeg;
    private bool MotionActivated = true;
    private bool FaceCamera = false;

    public Vector3D FaceCameraObjectAlignment;

    private ControllerState Inputs = new();

    private OverlayModelMode Modelmode;

    private static readonly ButtonFlags[] ButtonFlagsValues = Enum.GetValues<ButtonFlags>();
    private volatile bool _isVisible;

    private float ShoulderButtonsAngleDegLeftPrev;
    private float ShoulderButtonsAngleDegRightPrev;

    private float ShoulderTriggerAngleLeftPrev;
    private float ShoulderTriggerAngleRightPrev;

    private float TriggerAngleShoulderLeft;
    private float TriggerAngleShoulderRight;

    private readonly Transform3DGroup _modelTransformGroup = new();
    private readonly QuaternionRotation3D _devicePoseRotation;
    private readonly RotateTransform3D _deviceRotateTransform;
    private readonly AxisAngleRotation3D _restingPitchRotation;
    private readonly RotateTransform3D _restingPitchTransform;
    private readonly RotateTransform3D _importViewportCorrectionTransform;
    private readonly AxisAngleRotation3D _faceCameraRotationX;
    private readonly AxisAngleRotation3D _faceCameraRotationY;
    private readonly AxisAngleRotation3D _faceCameraRotationZ;
    private readonly RotateTransform3D _faceCameraTransformX;
    private readonly RotateTransform3D _faceCameraTransformY;
    private readonly RotateTransform3D _faceCameraTransformZ;
    private bool _faceCameraTransformsAttached;

    private readonly Dictionary<(Material DefaultMaterial, Material HighlightMaterial), DiffuseMaterial?[]> _gradientMaterialCache = [];
    private readonly JoystickTransformState _leftJoystickTransform = new();
    private readonly JoystickTransformState _rightJoystickTransform = new();
    private readonly ShoulderTransformState _leftShoulderTransform = new();
    private readonly ShoulderTransformState _rightShoulderTransform = new();

    public OverlayModel()
    {
        InitializeComponent();

        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        float samplePeriod = TimerManager.GetPeriod() / 1000f;
        madgwickAHRS = new(samplePeriod, 0.1f);

        // initialize timers
        UpdateTimer = new Timer(33)
        {
            AutoReset = true
        };
        UpdateTimer.Elapsed += DrawModel;

        _devicePoseRotation = new QuaternionRotation3D(DevicePose);
        _deviceRotateTransform = new RotateTransform3D(_devicePoseRotation);
        _restingPitchRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0.0d);
        _restingPitchTransform = new RotateTransform3D(_restingPitchRotation);
        _importViewportCorrectionTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), 90.0d));
        _faceCameraRotationX = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0.0d);
        _faceCameraRotationY = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0.0d);
        _faceCameraRotationZ = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0.0d);
        _faceCameraTransformX = new RotateTransform3D(_faceCameraRotationX);
        _faceCameraTransformY = new RotateTransform3D(_faceCameraRotationY);
        _faceCameraTransformZ = new RotateTransform3D(_faceCameraRotationZ);

        _modelTransformGroup.Children.Add(_deviceRotateTransform);
        _modelTransformGroup.Children.Add(_restingPitchTransform);
        _modelTransformGroup.Children.Add(_importViewportCorrectionTransform);
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "OverlayControllerMotion":
                MotionActivated = Convert.ToBoolean(value);
                break;
            case "OverlayFaceCamera":
                FaceCamera = Convert.ToBoolean(value);
                break;
            case "OverlayControllerRestingPitch":
                RestingPitchAngleDeg = (float)(-1.0d * Convert.ToDouble(value));
                break;
            case "OverlayControllerAlignment":
                {
                    int controllerAlignment = Convert.ToInt32(value);
                    // UI thread
                    UIHelper.TryInvoke(() => { UpdateUI_ControllerPosition(controllerAlignment); });
                }
                break;
            case "OverlayControllerSize":
                {
                    double controllerSize = Convert.ToDouble(value);
                    // UI thread
                    UIHelper.TryInvoke(() =>
                    {
                        MainWindow.overlayModel.Width = controllerSize;
                        MainWindow.overlayModel.Height = controllerSize;
                    });
                }
                break;
            case "OverlayModel":
                {
                    OverlayModelMode modelMode = (OverlayModelMode)Convert.ToInt32(value);
                    // UI thread
                    UIHelper.TryInvoke(() => { UpdateOverlayMode(modelMode); });
                }
                break;
            case "OverlayRenderAntialiasing":
                {
                    bool antialiasing = Convert.ToBoolean(value);
                    // UI thread
                    UIHelper.TryInvoke(() => { ModelViewPort.SetValue(RenderOptions.EdgeModeProperty, antialiasing ? EdgeMode.Unspecified : EdgeMode.Aliased); });
                }
                break;
            case "OverlayRenderInterval":
                {
                    double interval = 1000.0d / Convert.ToDouble(value);
                    UpdateInterval(interval);
                }
                break;
            case "OverlayControllerOpacity":
                {
                    double opacity = Convert.ToDouble(value);
                    // UI thread
                    UIHelper.TryInvoke(() => { ModelViewPort.Opacity = opacity; });
                }
                break;
            case "OverlayControllerAlwaysOnTop":
                {
                    bool alwaysOnTop = Convert.ToBoolean(value);
                    // UI thread
                    UIHelper.TryInvoke(() => { Topmost = alwaysOnTop; });
                }
                break;
            case "OverlayControllerBackgroundColor":
                {
                    Color SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(value));
                    // UI thread
                    UIHelper.TryInvoke(() => { Background = new SolidColorBrush(SelectedColor); });
                }
                break;
        }
    }

    private void UpdateUI_ControllerPosition(int controllerAlignment)
    {
        switch (controllerAlignment)
        {
            case 0:
            case 1:
            case 2:
                VerticalAlignment = VerticalAlignment.Top;
                break;
            case 3:
            case 4:
            case 5:
                VerticalAlignment = VerticalAlignment.Center;
                break;
            case 6:
            case 7:
            case 8:
                VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }

        switch (controllerAlignment)
        {
            case 0:
            case 3:
            case 6:
                HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case 1:
            case 4:
            case 7:
                HorizontalAlignment = HorizontalAlignment.Center;
                break;
            case 2:
            case 5:
            case 8:
                HorizontalAlignment = HorizontalAlignment.Right;
                break;
        }
    }

    private void ResetModelPose(GamepadMotion gamepadMotion)
    {
        // Reset model to initial pose
        FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d); // Angles when facing camera
        DevicePose = new Quaternion(0.0f, 0.0f, 0.0f, -1.0f);

        ShoulderButtonsAngleDegLeftPrev = 0;
        ShoulderButtonsAngleDegRightPrev = 0;

        ShoulderTriggerAngleLeftPrev = 0;
        ShoulderTriggerAngleRightPrev = 0;

        madgwickAHRS.Reset();
        gamepadMotion.Reset();
    }

    public void Close(bool v)
    {
        Close();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        UpdateTimer.Elapsed -= DrawModel;
        UpdateTimer.Stop();
        UpdateTimer.Dispose();
        CurrentModel?.Dispose();
        _gradientMaterialCache.Clear();
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

        if (IsLoaded)
            UpdateModel();
    }

    public void UpdateModel()
    {
        IModel newModel;

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
            CurrentModel?.Dispose();
            CurrentModel = newModel;
            ResetCachedVisualState();

            ModelVisual3D.Content = CurrentModel.model3DGroup;
            CurrentModel.model3DGroup.Transform = _modelTransformGroup;
            ModelViewPort.ZoomExtents();
        }
    }

    public void SetVisibility(Visibility visibility)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            this.Visibility = visibility;
            _isVisible = visibility == Visibility.Visible;
        });
    }

    public override void ToggleVisibility()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Visible:
                    _isVisible = false;
                    UpdateTimer.Stop();
                    Hide();
                    break;
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    if (CurrentModel is null)
                        UpdateModel();
                    _isVisible = true;
                    UpdateTimer.Start();
                    try { Show(); } catch { /* ItemsRepeater might have a NaN DesiredSize */ }
                    break;
            }
        }, DispatcherPriority.Normal);
    }

    #region ModelVisual3D

    private MadgwickAHRS madgwickAHRS;

    public void UpdateReport(ControllerState Inputs, GamepadMotion gamepadMotion, float deltaSeconds)
    {
        // Update only if 3D overlay is visible
        if (!_isVisible)
            return;

        this.Inputs = Inputs;

        // Return here if motion is not wanted for 3D model i.e. only update gamepad inputs
        if (!MotionActivated)
            return;

        // Reset device pose if all facebuttons are pressed at the same time
        if (AreResetButtonsPressed(Inputs))
            ResetModelPose(gamepadMotion);

        // Rotate for different coordinate system of 3D model and motion algorithm
        // Motion algorithm uses DS4 coordinate system
        // 3D model, has Z+ up, X+ to the right, Y+ towards the screen

        /*
        gamepadMotion.GetOrientation(out float oW, out float oX, out float oY, out float oZ);
        DevicePose = new Quaternion(-oX, -oY, oZ, oW);
        */

        gamepadMotion.GetCalibratedGyro(out float gyroX, out float gyroY, out float gyroZ);
        gamepadMotion.GetGravity(out float accelX, out float accelY, out float accelZ);

        // Update Madgwick orientation filter with IMU sensor data for 3D overlay
        madgwickAHRS.UpdateReport(
            -InputUtils.deg2rad(gyroX),
            InputUtils.deg2rad(gyroY),
            -InputUtils.deg2rad(gyroZ),
            -accelX,
            accelY,
            -accelZ,
            deltaSeconds
            );

        // System.Numerics to Media.3D, library really requires System.Numerics
        NumQuaternion quaternion = madgwickAHRS.GetQuaternion();
        DevicePose = new Quaternion(quaternion.W, quaternion.X, quaternion.Y, quaternion.Z);

        // Dirty fix for devices without Accelerometer (MSI Claw 8)
        if (gamepadMotion.accelX == 0 && gamepadMotion.accelY == 0 && gamepadMotion.accelZ == 0)
        {
            DevicePose.X = DevicePose.X * -1.0f;
            DevicePose.Z = DevicePose.Z * -1.0f;
        }

        // Also make euler equivalent availible of quaternions
        NumVector3 euler = InputUtils.ToEulerAngles(DevicePose);
        DevicePoseDeg = new Vector3D(
            InputUtils.rad2deg(euler.X),
            InputUtils.rad2deg(euler.Y),
            InputUtils.rad2deg(euler.Z));
    }

    private static bool AreResetButtonsPressed(ControllerState inputs)
    {
        if (inputs.ButtonState is null)
            return false;

        foreach (ButtonFlags button in resetFlags)
        {
            if (!inputs.ButtonState[button])
                return false;
        }

        return true;
    }

    private void DrawModel(object? sender, EventArgs e)
    {
        // Skip if we don't have a model or overlay is not visible
        if (CurrentModel is null || !_isVisible)
            return;

        // UI thread
        UIHelper.TryInvoke(() =>
        {
            // Snapshot current model to avoid race conditions during model switches
            IModel model = CurrentModel;
            if (model is null)
                return;

            // Color buttons based on pressed state
            foreach (ButtonFlags button in ButtonFlagsValues)
            {
                if (!model.ButtonMap.TryGetValue(button, out var buttonModels))
                    continue;

                bool hasButton = Inputs.ButtonState is not null && Inputs.ButtonState[button];

                foreach (Model3DGroup model3DGroup in buttonModels)
                {
                    GeometryModel3D? model3D = model3DGroup.Children[0] as GeometryModel3D;
                    if (model3D is null || model3D.Material is not DiffuseMaterial)
                        continue;

                    Material targetMaterial = hasButton ? model.HighlightMaterials[model3DGroup] : model.DefaultMaterials[model3DGroup];
                    SetMaterialIfChanged(model3D, targetMaterial, updateBackMaterial: true);
                }
            }

            _devicePoseRotation.Quaternion = DevicePose;

            // User requested resting pitch of device around X axis, invert when motion but not face camera is enabled
            float direction = MotionActivated && !FaceCamera ? -1.0f : 1.0f;
            _restingPitchRotation.Angle = direction * RestingPitchAngleDeg;

            // Devices rotates (slowly) towards a default position facing the camara 
            bool faceCameraEnabled = FaceCamera && MotionActivated;
            EnsureFaceCameraTransforms(faceCameraEnabled);
            if (faceCameraEnabled)
            {
                float faceCameraAlignmentFactor = (float)Math.Clamp(UpdateTimer.Interval * _faceCameraAlignmentRatePerSecond / 1000.0d, 0.0d, 1.0d);

                // Determine diff angles
                DiffAngle.X = DevicePoseDeg.X - FaceCameraObjectAlignment.X -
                          (float)DesiredAngleDeg.X;
                DiffAngle.Y = DevicePoseDeg.Y - FaceCameraObjectAlignment.Y -
                          (float)DesiredAngleDeg.Y;
                DiffAngle.Z = DevicePoseDeg.Z - FaceCameraObjectAlignment.Z -
                          (float)DesiredAngleDeg.Z;

                // Correction amount for camera, scaled by render interval for more consistent responsiveness
                FaceCameraObjectAlignment += DiffAngle * faceCameraAlignmentFactor;

                // Apply face camera correction angles to the XYZ axis
                _faceCameraRotationX.Angle = -FaceCameraObjectAlignment.X;
                _faceCameraRotationY.Angle = FaceCameraObjectAlignment.Y;
                _faceCameraRotationZ.Angle = -FaceCameraObjectAlignment.Z;
            }

            // Upward rotation for shoulder buttons angle to compensate for visiblity
            float modelPoseXDeg = 0.0f;

            // Determine the model's pose based motion, face camera and resting pitch
            if (MotionActivated)
            {
                // Start by setting the model pose based on the device's orientation adjusted by the desired angle from the UI
                modelPoseXDeg = (float)DevicePoseDeg.Z - RestingPitchAngleDeg;

                // If the model should face the camera, further adjust by the camera object's alignment.
                if (FaceCamera)
                {
                    modelPoseXDeg -= (float)FaceCameraObjectAlignment.Z;
                }
            }
            else
            {
                // If motion is not activated, set the model's pose to the desired UI pitch angle directly
                modelPoseXDeg = RestingPitchAngleDeg;
            }

            // Rotate shoulder buttons based on modelPoseXDeg
            float ShoulderButtonsAngleDeg = 0.0f;

            if (modelPoseXDeg < 0)
            {
                ShoulderButtonsAngleDeg = Math.Clamp(90.0f - modelPoseXDeg, 90.0f, 180.0f);
            }
            else if (modelPoseXDeg <= 45.0f)
            {
                ShoulderButtonsAngleDeg = 90.0f - (2.0f * modelPoseXDeg);
            }

            // Update shoulder buttons left, rotate into view angle, trigger angle and trigger gradient color
            Model3DGroup leftShoulderButton = model.ButtonMap[ButtonFlags.L1][0];
            UpdateShoulderButtons(
                _leftShoulderTransform,
                model.LeftShoulderTrigger,
                leftShoulderButton,
                model.UpwardVisibilityRotationAxisLeft,
                model.UpwardVisibilityRotationPointLeft,
                model.ShoulderTriggerRotationPointCenterLeftMillimeter,
                ref TriggerAngleShoulderLeft,
                model.TriggerMaxAngleDeg,
                AxisFlags.L2,
                ShoulderButtonsAngleDeg,
                ref ShoulderTriggerAngleLeftPrev,
                ref ShoulderButtonsAngleDegLeftPrev,
                model.DefaultMaterials[model.LeftShoulderTrigger],
                model.HighlightMaterials
            );

            // Update shoulder buttons right, rotate into view angle, trigger angle and trigger gradient color
            Model3DGroup rightShoulderButton = model.ButtonMap[ButtonFlags.R1][0];
            UpdateShoulderButtons(
                _rightShoulderTransform,
                model.RightShoulderTrigger,
                rightShoulderButton,
                model.UpwardVisibilityRotationAxisRight,
                model.UpwardVisibilityRotationPointRight,
                model.ShoulderTriggerRotationPointCenterRightMillimeter,
                ref TriggerAngleShoulderRight,
                model.TriggerMaxAngleDeg,
                AxisFlags.R2,
                ShoulderButtonsAngleDeg,
                ref ShoulderTriggerAngleRightPrev,
                ref ShoulderButtonsAngleDegRightPrev,
                model.DefaultMaterials[model.RightShoulderTrigger],
                model.HighlightMaterials
            );

            // Update left joystick
            UpdateJoystick(
                AxisFlags.LeftStickX,
                AxisFlags.LeftStickY,
                model.LeftThumbRing,
                model.LeftThumb,
                model.JoystickRotationPointCenterLeftMillimeter,
                model.JoystickMaxAngleDeg,
                model.DefaultMaterials,
                model.HighlightMaterials,
                _leftJoystickTransform);

            // Update right joystick
            UpdateJoystick(
                AxisFlags.RightStickX,
                AxisFlags.RightStickY,
                model.RightThumbRing,
                model.RightThumb,
                model.JoystickRotationPointCenterRightMillimeter,
                model.JoystickMaxAngleDeg,
                model.DefaultMaterials,
                model.HighlightMaterials,
                _rightJoystickTransform);
        }, DispatcherPriority.Render);
    }

    private void ResetCachedVisualState()
    {
        _gradientMaterialCache.Clear();
        _leftJoystickTransform.Reset();
        _rightJoystickTransform.Reset();
        _leftShoulderTransform.Reset();
        _rightShoulderTransform.Reset();
        EnsureFaceCameraTransforms(false);

        ShoulderButtonsAngleDegLeftPrev = float.NaN;
        ShoulderButtonsAngleDegRightPrev = float.NaN;
        ShoulderTriggerAngleLeftPrev = float.NaN;
        ShoulderTriggerAngleRightPrev = float.NaN;
    }

    private void EnsureFaceCameraTransforms(bool enabled)
    {
        if (enabled == _faceCameraTransformsAttached)
            return;

        if (enabled)
        {
            _modelTransformGroup.Children.Add(_faceCameraTransformX);
            _modelTransformGroup.Children.Add(_faceCameraTransformY);
            _modelTransformGroup.Children.Add(_faceCameraTransformZ);
        }
        else
        {
            _modelTransformGroup.Children.Remove(_faceCameraTransformX);
            _modelTransformGroup.Children.Remove(_faceCameraTransformY);
            _modelTransformGroup.Children.Remove(_faceCameraTransformZ);
        }

        _faceCameraTransformsAttached = enabled;
    }

    private void SetMaterialIfChanged(GeometryModel3D geometryModel3D, Material material, bool updateBackMaterial = false)
    {
        if (!ReferenceEquals(geometryModel3D.Material, material))
            geometryModel3D.Material = material;

        if (updateBackMaterial && !ReferenceEquals(geometryModel3D.BackMaterial, material))
            geometryModel3D.BackMaterial = material;
    }

    private Material GradientHighlight(Material defaultMaterial, Material highlightMaterial, float factor)
    {
        if (factor <= 0.0f || defaultMaterial is not DiffuseMaterial defaultDiffuse || highlightMaterial is not DiffuseMaterial highlightDiffuse)
            return defaultMaterial;

        if (factor >= 1.0f)
            return highlightMaterial;

        int cacheIndex = (int)Math.Clamp(MathF.Round(factor * byte.MaxValue), 0.0f, byte.MaxValue);
        if (cacheIndex == 0)
            return defaultMaterial;

        if (cacheIndex == byte.MaxValue)
            return highlightMaterial;

        var cacheKey = (defaultMaterial, highlightMaterial);
        if (!_gradientMaterialCache.TryGetValue(cacheKey, out DiffuseMaterial?[]? cache))
        {
            cache = new DiffuseMaterial?[byte.MaxValue + 1];
            _gradientMaterialCache[cacheKey] = cache;
        }

        DiffuseMaterial? material = cache[cacheIndex];
        if (material is not null)
            return material;

        Color startColor = ((SolidColorBrush)defaultDiffuse.Brush).Color;
        Color endColor = ((SolidColorBrush)highlightDiffuse.Brush).Color;

        // Interpolate colors
        Color transitionColor = InterpolateColor(startColor, endColor, factor);

        SolidColorBrush transitionBrush = new SolidColorBrush(transitionColor);
        if (transitionBrush.CanFreeze)
            transitionBrush.Freeze();

        material = new DiffuseMaterial(transitionBrush);
        if (material.CanFreeze)
            material.Freeze();

        cache[cacheIndex] = material;
        return material;
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
    Dictionary<Model3DGroup, Material> highlightMaterials,
    JoystickTransformState transformState)
    {
        GeometryModel3D? geometryModel3D = thumbRing.Children[0] as GeometryModel3D;
        if (geometryModel3D is null)
            return;

        float X = Inputs.AxisState is not null ? Inputs.AxisState[stickX] : 0.0f;
        float Y = Inputs.AxisState is not null ? Inputs.AxisState[stickY] : 0.0f;

        // Determine if stick has moved
        bool isStickMoved = X != 0.0f || Y != 0.0f;

        // Adjust material gradually based on distance from center
        if (isStickMoved)
        {
            float gradientFactor = MathF.Max(
                MathF.Abs(X * ShortMaxValueInverse),
                MathF.Abs(Y * ShortMaxValueInverse));

            SetMaterialIfChanged(geometryModel3D, GradientHighlight(
                defaultMaterials[thumbRing],
                highlightMaterials[thumbRing],
                gradientFactor));
        }
        else
        {
            SetMaterialIfChanged(geometryModel3D, defaultMaterials[thumbRing]);
        }

        // Define and compute rotation angles
        float x = maxAngleDeg * X * ShortMaxValueInverse;
        float y = -maxAngleDeg * Y * ShortMaxValueInverse;

        transformState.Attach(thumbRing, thumb, rotationPointCenter);
        transformState.UpdateAngles(x, y);
    }

    // Shoulder buttons, rotate into view angle, trigger angle and trigger gradient color
    private void UpdateShoulderButtons(
        ShoulderTransformState transformState,
        Model3DGroup shoulderTriggerModel,
        Model3DGroup shoulderButtonModel,
        Vector3D upwardVisibilityRotationAxis,
        Vector3D upwardVisibilityRotationPoint,
        Vector3D shoulderTriggerRotationPoint,
        ref float triggerAngle,
        float triggerMaxAngleDeg,
        AxisFlags triggerFlag,
        float shoulderButtonsAngleDeg,
        ref float triggerAnglePrev,
        ref float shoulderButtonsAngleDegPrev,
        Material defaultMaterial,
        Dictionary<Model3DGroup, Material> highlightMaterials)
    {
        GeometryModel3D? geometryModel3D = shoulderTriggerModel.Children[0] as GeometryModel3D;
        if (geometryModel3D is null)
            return;

        float triggerValue = Inputs.AxisState is not null ? (Inputs.AxisState[triggerFlag] * ByteMaxValueInverse) : 0.0f;

        // Adjust trigger color gradient based on amount of pull
        if (triggerValue > 0)
        {
            SetMaterialIfChanged(geometryModel3D, GradientHighlight(defaultMaterial, highlightMaterials[shoulderTriggerModel], triggerValue));
        }
        else
        {
            SetMaterialIfChanged(geometryModel3D, defaultMaterial);
        }

        // Determine trigger angle pull in degree
        triggerAngle = -triggerMaxAngleDeg * triggerValue;

        // In case device pose changes leading to different visibility angle or if trigger angle changes
        if (shoulderButtonsAngleDeg != shoulderButtonsAngleDegPrev || triggerAngle != triggerAnglePrev)
        {
            transformState.Attach(shoulderTriggerModel, shoulderButtonModel);
            transformState.Update(
                upwardVisibilityRotationAxis,
                upwardVisibilityRotationPoint,
                shoulderButtonsAngleDeg,
                shoulderTriggerRotationPoint,
                triggerAngle);

            triggerAnglePrev = triggerAngle;
            shoulderButtonsAngleDegPrev = shoulderButtonsAngleDeg;
        }

    }

    private sealed class JoystickTransformState
    {
        private readonly AxisAngleRotation3D _rotationX = new(new Vector3D(0, 0, 1), 0.0d);
        private readonly AxisAngleRotation3D _rotationY = new(new Vector3D(1, 0, 0), 0.0d);
        private readonly RotateTransform3D _rotateTransformX;
        private readonly RotateTransform3D _rotateTransformY;
        private readonly Transform3DGroup _transformGroup = new();
        private Model3DGroup _thumbRing;
        private Model3D _thumb;

        public JoystickTransformState()
        {
            _rotateTransformX = new RotateTransform3D(_rotationX);
            _rotateTransformY = new RotateTransform3D(_rotationY);
            _transformGroup.Children.Add(_rotateTransformX);
            _transformGroup.Children.Add(_rotateTransformY);
        }

        public void Attach(Model3DGroup thumbRing, Model3D thumb, Vector3D rotationPointCenter)
        {
            if (!ReferenceEquals(_thumbRing, thumbRing) || !ReferenceEquals(_thumb, thumb))
            {
                _thumbRing = thumbRing;
                _thumb = thumb;
                thumbRing.Transform = _transformGroup;
                thumb.Transform = _transformGroup;
            }

            _rotateTransformX.CenterX = rotationPointCenter.X;
            _rotateTransformX.CenterY = rotationPointCenter.Y;
            _rotateTransformX.CenterZ = rotationPointCenter.Z;
            _rotateTransformY.CenterX = rotationPointCenter.X;
            _rotateTransformY.CenterY = rotationPointCenter.Y;
            _rotateTransformY.CenterZ = rotationPointCenter.Z;
        }

        public void UpdateAngles(float x, float y)
        {
            if (_rotationX.Angle != x)
                _rotationX.Angle = x;

            if (_rotationY.Angle != y)
                _rotationY.Angle = y;
        }

        public void Reset()
        {
            _thumbRing = null;
            _thumb = null;
            _rotationX.Angle = 0.0d;
            _rotationY.Angle = 0.0d;
        }
    }

    private sealed class ShoulderTransformState
    {
        private readonly AxisAngleRotation3D _shoulderRotation = new(new Vector3D(0, 0, 1), 0.0d);
        private readonly AxisAngleRotation3D _triggerRotation = new(new Vector3D(0, 0, 1), 0.0d);
        private readonly RotateTransform3D _shoulderTransform;
        private readonly RotateTransform3D _triggerTransform;
        private readonly Transform3DGroup _triggerTransformGroup = new();
        private Model3DGroup _triggerModel;
        private Model3DGroup _buttonModel;

        public ShoulderTransformState()
        {
            _shoulderTransform = new RotateTransform3D(_shoulderRotation);
            _triggerTransform = new RotateTransform3D(_triggerRotation);
            _triggerTransformGroup.Children.Add(_triggerTransform);
            _triggerTransformGroup.Children.Add(_shoulderTransform);
        }

        public void Attach(Model3DGroup triggerModel, Model3DGroup buttonModel)
        {
            if (!ReferenceEquals(_triggerModel, triggerModel))
            {
                _triggerModel = triggerModel;
                triggerModel.Transform = _triggerTransformGroup;
            }

            if (!ReferenceEquals(_buttonModel, buttonModel))
            {
                _buttonModel = buttonModel;
                buttonModel.Transform = _shoulderTransform;
            }
        }

        public void Update(
            Vector3D upwardVisibilityRotationAxis,
            Vector3D upwardVisibilityRotationPoint,
            float shoulderButtonsAngleDeg,
            Vector3D shoulderTriggerRotationPoint,
            float shoulderTriggerAngleDeg)
        {
            _shoulderRotation.Axis = upwardVisibilityRotationAxis;
            _shoulderRotation.Angle = shoulderButtonsAngleDeg;
            _shoulderTransform.CenterX = upwardVisibilityRotationPoint.X;
            _shoulderTransform.CenterY = upwardVisibilityRotationPoint.Y;
            _shoulderTransform.CenterZ = upwardVisibilityRotationPoint.Z;

            _triggerRotation.Axis = upwardVisibilityRotationAxis;
            _triggerRotation.Angle = shoulderTriggerAngleDeg;
            _triggerTransform.CenterX = shoulderTriggerRotationPoint.X;
            _triggerTransform.CenterY = shoulderTriggerRotationPoint.Y;
            _triggerTransform.CenterZ = shoulderTriggerRotationPoint.Z;
        }

        public void Reset()
        {
            _triggerModel = null;
            _buttonModel = null;
            _shoulderRotation.Angle = 0.0d;
            _triggerRotation.Angle = 0.0d;
        }
    }

    #endregion
}