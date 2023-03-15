using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
using HandheldCompanion.Views.Classes;
using PrecisionTiming;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class OverlayModel : OverlayWindow
    {
        private PrecisionTimer UpdateTimer;

        private ControllerState Inputs = new();
        private ButtonState prevState = new();

        private IModel CurrentModel;
        private OverlayModelMode Modelmode;
        private HIDmode HIDmode;

        private GeometryModel3D model;

        public bool FaceCamera = false;
        public Vector3D FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d);
        public bool MotionActivated = true;
        public Vector3D DesiredAngleDeg = new Vector3D(0, 0, 0);
        private Quaternion DevicePose = new Quaternion(0.0f, 0.0f, 1.0f, 0.0f);
        private Vector3D DevicePoseRad = new Vector3D(0, 3.14, 0);
        private Vector3D DiffAngle = new Vector3D(0, 0, 0);
        Transform3DGroup Transform3DGroupModelPrevious = new Transform3DGroup();

        // TODO Dummy variables, placeholder and for testing 
        private short MotorLeftPlaceholder;
        private short MotorRightPlaceholder;

        private float TriggerAngleShoulderLeft;
        private float TriggerAngleShoulderLeftPrev;
        private float TriggerAngleShoulderRight;
        private float TriggerAngleShoulderRightPrev;
        private float ShoulderButtonsAngleDegPrev;

        public OverlayModel()
        {
            InitializeComponent();

            PipeClient.ServerMessage += OnServerMessage;
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // initialize timers
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetAutoResetMode(true);
            UpdateTimer.Tick += DrawModel;

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
                        FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d);
                        DevicePose = new Quaternion(0.0f, 0.0f, 1.0f, 0.0f);
                        DevicePoseRad = new Vector3D(0, 3.14, 0);
                        break;
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateTimer.Tick -= DrawModel;
            UpdateTimer.Stop();
        }

        public void UpdateInterval(double interval)
        {
            UpdateTimer.Stop();
            UpdateTimer.SetInterval((int)interval);
            UpdateTimer.Start();
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

        public override void UpdateVisibility()
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (Visibility)
                {
                    case Visibility.Visible:
                        this.Hide();
                        break;
                    case Visibility.Collapsed:
                    case Visibility.Hidden:
                        this.Show();
                        break;
                }

                PipeClient.SendMessage(new PipeOverlay((int)Visibility));
            });
        }

        #region ModelVisual3D
        private RotateTransform3D DeviceRotateTransform;
        private RotateTransform3D DeviceRotateTransformFaceCameraX;
        private RotateTransform3D DeviceRotateTransformFaceCameraY;
        private RotateTransform3D DeviceRotateTransformFaceCameraZ;
        private RotateTransform3D LeftJoystickRotateTransform;
        private RotateTransform3D RightJoystickRotateTransform;

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SENSOR:
                    {
                        // prevent late PipeMessage to apply
                        if (this.Visibility != Visibility.Visible)
                            return;

                        // Add return here if motion is not wanted for 3D model
                        if (!MotionActivated)
                            return;

                        PipeSensor sensor = (PipeSensor)message;
                        switch (sensor.type)
                        {
                            case SensorType.Quaternion:
                                DevicePose = new Quaternion(sensor.q_w, sensor.q_x, sensor.q_y, sensor.q_z);
                                DevicePoseRad = new Vector3D(sensor.x, sensor.y, sensor.z);
                                break;
                        }
                    }
                    break;
            }
        }

        private void DrawModel(object? sender, EventArgs e)
        {
            if (CurrentModel is null)
                return;

            // skip virtual controller update if hidden or collapsed
            if (Visibility != Visibility.Visible)
                return;

            if (!prevState.Equals(Inputs.ButtonState))
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    GeometryModel3D model = null;
                    foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
                    {
                        if (!CurrentModel.ButtonMap.ContainsKey(button))
                            continue;

                        foreach (Model3DGroup modelgroup in CurrentModel.ButtonMap[button])
                        {
                            model = (GeometryModel3D)modelgroup.Children.FirstOrDefault();

                            if (model.Material.GetType() != typeof(DiffuseMaterial))
                                continue;

                            if (Inputs.ButtonState[button])
                                model.Material = model.BackMaterial = CurrentModel.HighlightMaterials[modelgroup];
                            else
                                model.Material = model.BackMaterial = CurrentModel.DefaultMaterials[modelgroup];
                        }
                    }
                });

                prevState = Inputs.ButtonState as ButtonState;
            }

            // update model
            UpdateModelVisual3D();

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                float GradientFactor; // Used for multiple models

                // TODO update motor placeholders!
                // Motor Left
                model = CurrentModel.LeftMotor.Children[0] as GeometryModel3D;
                model.Material = MotorLeftPlaceholder > 0 ? CurrentModel.HighlightMaterials[CurrentModel.LeftMotor] : CurrentModel.DefaultMaterials[CurrentModel.LeftMotor];

                // Motor Right
                model = CurrentModel.RightMotor.Children[0] as GeometryModel3D;
                model.Material = MotorRightPlaceholder > 0 ? CurrentModel.HighlightMaterials[CurrentModel.RightMotor] : CurrentModel.DefaultMaterials[CurrentModel.RightMotor];

                // ShoulderLeftTrigger
                model = CurrentModel.LeftShoulderTrigger.Children[0] as GeometryModel3D;
                if (Inputs.AxisState[AxisFlags.L2] > 0)
                {
                    GradientFactor = 1 * Inputs.AxisState[AxisFlags.L2] / (float)byte.MaxValue;
                    model.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger],
                                                       CurrentModel.HighlightMaterials[CurrentModel.LeftShoulderTrigger],
                                                       GradientFactor);
                }
                else
                {
                    model.Material = CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger];
                }

                TriggerAngleShoulderLeft = -1 * CurrentModel.TriggerMaxAngleDeg * Inputs.AxisState[AxisFlags.L2] / (float)byte.MaxValue;

                // ShoulderRightTrigger
                model = CurrentModel.RightShoulderTrigger.Children[0] as GeometryModel3D;

                if (Inputs.AxisState[AxisFlags.R2] > 0)
                {
                    GradientFactor = 1 * Inputs.AxisState[AxisFlags.R2] / (float)byte.MaxValue;
                    model.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger],
                                                       CurrentModel.HighlightMaterials[CurrentModel.RightShoulderTrigger],
                                                       GradientFactor);
                }
                else
                {
                    model.Material = CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger];
                }

                TriggerAngleShoulderRight = -1 * CurrentModel.TriggerMaxAngleDeg * Inputs.AxisState[AxisFlags.R2] / (float)byte.MaxValue;

                // JoystickLeftRing
                model = CurrentModel.LeftThumbRing.Children[0] as GeometryModel3D;
                if (Inputs.AxisState[AxisFlags.LeftThumbX] != 0.0f || Inputs.AxisState[AxisFlags.LeftThumbY] != 0.0f)
                {
                    // Adjust color
                    GradientFactor = Math.Max(Math.Abs(1 * Inputs.AxisState[AxisFlags.LeftThumbX] / (float)short.MaxValue),
                                              Math.Abs(1 * Inputs.AxisState[AxisFlags.LeftThumbY] / (float)short.MaxValue));

                    model.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.LeftThumbRing],
                                                       CurrentModel.HighlightMaterials[CurrentModel.LeftThumbRing],
                                                       GradientFactor);

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickLeft = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.LeftThumbX] / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.LeftThumbY] / (float)short.MaxValue;

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
                    model.Material = CurrentModel.DefaultMaterials[CurrentModel.LeftThumbRing];

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
                model = CurrentModel.RightThumbRing.Children[0] as GeometryModel3D;
                if (Inputs.AxisState[AxisFlags.RightThumbX] != 0.0f || Inputs.AxisState[AxisFlags.RightThumbY] != 0.0f)
                {
                    // Adjust color
                    GradientFactor = Math.Max(Math.Abs(1 * Inputs.AxisState[AxisFlags.RightThumbX] / (float)short.MaxValue),
                                              Math.Abs(1 * Inputs.AxisState[AxisFlags.RightThumbY] / (float)short.MaxValue));

                    model.Material = GradientHighlight(CurrentModel.DefaultMaterials[CurrentModel.RightThumbRing],
                                                       CurrentModel.HighlightMaterials[CurrentModel.RightThumbRing],
                                                       GradientFactor);

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickRight = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.RightThumbX] / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * Inputs.AxisState[AxisFlags.RightThumbY] / (float)short.MaxValue;

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
                    CurrentModel.RightThumbRing.Transform = CurrentModel.RightThumb.Transform = Transform3DGroupJoystickRight;

                }
                else
                {
                    model.Material = CurrentModel.DefaultMaterials[CurrentModel.RightThumbRing];

                    // Define and compute, back to default position
                    var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                    RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightJoystickRotateTransform.CenterX = CurrentModel.JoystickRotationPointCenterRightMillimeter.X;
                    RightJoystickRotateTransform.CenterY = CurrentModel.JoystickRotationPointCenterRightMillimeter.Y;
                    RightJoystickRotateTransform.CenterZ = CurrentModel.JoystickRotationPointCenterRightMillimeter.Z;

                    // Transform joystick
                    CurrentModel.RightThumbRing.Transform = CurrentModel.RightThumb.Transform = RightJoystickRotateTransform;
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
            Brush DefaultMaterialBrush = ((DiffuseMaterial)DefaultMaterial).Brush;
            Color StartColor = ((SolidColorBrush)DefaultMaterialBrush).Color;
            Brush HighlightMaterialBrush = ((DiffuseMaterial)HighlightMaterial).Brush;
            Color EndColor = ((SolidColorBrush)HighlightMaterialBrush).Color;

            // Linear interpolate color
            float bk = (1 - Factor);
            float a = StartColor.A * bk + EndColor.A * Factor;
            float r = StartColor.R * bk + EndColor.R * Factor;
            float g = StartColor.G * bk + EndColor.G * Factor;
            float b = StartColor.B * bk + EndColor.B * Factor;

            // Define color
            Color TransitionColor = Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);

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
            Transform3DGroup Transform3DGroupShoulderTrigger = new Transform3DGroup();

            // Upward visibility rotation vector and angle
            var ax3d = new AxisAngleRotation3D(UpwardVisibilityRotationAxis, ShoulderButtonsAngleDeg);
            RotateTransform3D TransformShoulder = new RotateTransform3D(ax3d);

            // Define rotation point shoulder buttons
            TransformShoulder.CenterX = UpwardVisibilityRotationPoint.X;
            TransformShoulder.CenterY = UpwardVisibilityRotationPoint.Y;
            TransformShoulder.CenterZ = UpwardVisibilityRotationPoint.Z;

            // Trigger vector and angle
            ax3d = new AxisAngleRotation3D(UpwardVisibilityRotationAxis, ShoulderTriggerAngleDeg);
            RotateTransform3D TransformTriggerPosition = new RotateTransform3D(ax3d);

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

        private void UpdateModelVisual3D()
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                Transform3DGroup Transform3DGroupModel = new Transform3DGroup();

                // Device transformation based on pose
                var Ax3DDevicePose = new QuaternionRotation3D(DevicePose);
                DeviceRotateTransform = new RotateTransform3D(Ax3DDevicePose);
                Transform3DGroupModel.Children.Add(DeviceRotateTransform);

                // Determine diff angles
                DiffAngle.X = (InputUtils.rad2deg((float)DevicePoseRad.X) - (float)FaceCameraObjectAlignment.X) - (float)DesiredAngleDeg.X;
                DiffAngle.Y = (InputUtils.rad2deg((float)DevicePoseRad.Y) - (float)FaceCameraObjectAlignment.Y) - (float)DesiredAngleDeg.Y;
                DiffAngle.Z = (InputUtils.rad2deg((float)DevicePoseRad.Z) - (float)FaceCameraObjectAlignment.Z) - (float)DesiredAngleDeg.Z;

                // Handle wrap around at -180 +180 position which is horizontal for steering
                DiffAngle.Y = ((float)DevicePoseRad.Y < 0.0) ? DiffAngle.Y += 180.0f : DiffAngle.Y -= 180.0f;

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

                float ModelPoseXDeg = 0.0f;

                if (FaceCamera)
                {
                    ModelPoseXDeg = InputUtils.rad2deg((float)DevicePoseRad.X) - (float)FaceCameraObjectAlignment.X;
                }
                else
                {
                    // Not slowly rotate into view when face camera is off
                    ModelPoseXDeg = InputUtils.rad2deg((float)DevicePoseRad.X);
                }

                float ShoulderButtonsAngleDeg = 0.0f;

                // Rotate shoulder 90 degrees upward while controller faces user
                if (ModelPoseXDeg < 0)
                {
                    ShoulderButtonsAngleDeg = Math.Clamp(90.0f - (1 * ModelPoseXDeg), 90.0f, 180.0f);
                }
                // In between rotate inverted from pose
                else if (ModelPoseXDeg >= 0 && ModelPoseXDeg <= 45.0f)
                {
                    ShoulderButtonsAngleDeg = 90.0f - (2 * ModelPoseXDeg);
                }
                // Rotate shoulder buttons to original spot at -45 and beyond
                else if (ModelPoseXDeg < 45.0f)
                {
                    ShoulderButtonsAngleDeg = 0.0f;
                }
                Model3DGroup Placeholder = new Model3DGroup();

                // Left shoulder buttons visibility rotation and trigger button angle 
                // only perform when model or triggers are in a different position 
                if (ShoulderButtonsAngleDeg != ShoulderButtonsAngleDegPrev || TriggerAngleShoulderLeft != TriggerAngleShoulderLeftPrev)
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
                if (ShoulderButtonsAngleDeg != ShoulderButtonsAngleDegPrev || TriggerAngleShoulderRight != TriggerAngleShoulderRightPrev)
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

            });
        }
        #endregion
    }
}
