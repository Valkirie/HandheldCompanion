using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using TouchEventSample;
using static TouchEventSample.TouchSourceWinTouch;
using GamepadButtonFlags = SharpDX.XInput.GamepadButtonFlags;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class Overlay : Window
    {
        #region import
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        #endregion

        private ILogger logger;
        private PipeClient pipeClient;

        private Model CurrentModel;
        private Model ProductModel;
        private Model VirtualModel;
        private Model BonusModel;

        private OverlayModelMode ModelMode;

        private Point OverlayPosition;
        private Point LeftTrackPadPosition;
        private Point RightTrackPadPosition;

        private enum TouchTarget
        {
            SwipeTop = 0,
            TrackpadLeft = 1,
            TrackpadRight = 2
        }

        private TouchSourceWinTouch touchsource;
        private long prevLeftTrackPadTime;
        private long prevRightTrackPadTime;
        private TouchTarget target;
        private TouchArgs swipe;

        // Gamepad vars
        private MultimediaTimer UpdateTimer;
        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private State GamepadState;
        private bool ControllerTriggerListening = false;
        private bool TrackpadsTriggerListening = false;

        private Vector3D FaceCameraObjectAlignment = new Vector3D(0.0d, 0.0d, 0.0d);

        public event ControllerTriggerUpdatedEventHandler ControllerTriggerUpdated;
        public delegate void ControllerTriggerUpdatedEventHandler(GamepadButtonFlags button);

        public event TrackpadsTriggerUpdatedEventHandler TrackpadsTriggerUpdated;
        public delegate void TrackpadsTriggerUpdatedEventHandler(GamepadButtonFlags button);

        private float TriggerAngleShoulderLeft;
        private float TriggerAngleShoulderRight;

        private bool controllerTriggered = false;
        private bool trackpadTriggered = false;

        public GamepadButtonFlags controllerTriggerButtons = GamepadButtonFlags.Back | GamepadButtonFlags.DPadUp;
        public GamepadButtonFlags trackpadTriggerButtons = GamepadButtonFlags.Back | GamepadButtonFlags.DPadDown;

        // TODO Dummy variables, placeholder and for testing 
        short MotorLeftPlaceholder;
        short MotorRightPlaceholder;

        public Overlay()
        {
            InitializeComponent();

            // touch vars
            touchsource = new TouchSourceWinTouch(this);
            touchsource.Touch += Touchsource_Touch;

            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
            UpdateTimer.Tick += UpdateReport;
            UpdateTimer.Start();

            this.SourceInitialized += Overlay_SourceInitialized;
        }

        public Overlay(ILogger logger, PipeClient pipeClient) : this()
        {
            this.logger = logger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateTimer.Tick -= UpdateReport;
            UpdateTimer.Stop();
        }

        public void UpdateProductModel(Model ProductModel)
        {
            this.ProductModel = ProductModel;
            UpdateModel();
        }

        public void UpdateVirtualModel(Model VirtualModel)
        {
            this.VirtualModel = VirtualModel;
            UpdateModel();
        }

        public void UpdateBonusModel(Model BonusModel)
        {
            this.BonusModel = BonusModel;
            UpdateModel();
        }

        public void UpdateModelMode(OverlayModelMode ModelMode)
        {
            this.ModelMode = ModelMode;
            UpdateModel();
        }

        public void UpdateModel()
        {
            switch (this.ModelMode)
            {
                case OverlayModelMode.OEM:
                    if (ProductModel != null)
                    {
                        CurrentModel = ProductModel;
                        ModelVisual3D.Content = ProductModel.model3DGroup;
                    }
                    else goto case OverlayModelMode.Virtual;
                    break;
                case OverlayModelMode.Virtual:
                    if (VirtualModel != null)
                    {
                        CurrentModel = VirtualModel;
                        ModelVisual3D.Content = VirtualModel.model3DGroup;
                    }
                    break;
                case OverlayModelMode.Toy:
                    if (BonusModel != null)
                    {
                        CurrentModel = BonusModel;
                        ModelVisual3D.Content = BonusModel.model3DGroup;
                    }
                    break;
            }

            ModelViewPort.ZoomExtents();
        }

        private void Overlay_SourceInitialized(object? sender, EventArgs e)
        {
            //Set the window style to noactivate.
            WindowInteropHelper helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
        }

        private void Touchsource_Touch(TouchArgs args, long time)
        {
            double X = args.LocationX - this.OverlayPosition.X;
            double Y = args.LocationY - this.OverlayPosition.Y;

            double CenterX = this.ActualWidth / 2;
            target = X < CenterX ? TouchTarget.TrackpadLeft : TouchTarget.TrackpadRight;

            CursorButton Button = CursorButton.None;
            Point CurrentPoint;

            switch (target)
            {
                default:
                case TouchTarget.TrackpadLeft:
                    Button = CursorButton.TouchLeft;
                    CurrentPoint = LeftTrackPadPosition;
                    break;
                case TouchTarget.TrackpadRight:
                    Button = CursorButton.TouchRight;
                    CurrentPoint = RightTrackPadPosition;
                    break;
                case TouchTarget.SwipeTop:
                    if (args.Status == CursorEvent.EventType.DOWN)
                        swipe = args;
                    return;
            }

            // normalized
            var relativeX = Math.Clamp(args.LocationX - CurrentPoint.X, 0, LeftTrackpad.ActualWidth);
            var relativeY = Math.Clamp(args.LocationY - CurrentPoint.Y, 0, LeftTrackpad.ActualHeight);

            var normalizedX = (relativeX / LeftTrackpad.ActualWidth) / 2.0d;
            var normalizedY = relativeY / LeftTrackpad.ActualHeight;

            switch (target)
            {
                default:
                case TouchTarget.TrackpadLeft:
                    {
                        if (args.Status == CursorEvent.EventType.DOWN)
                        {
                            LeftTrackpad.Opacity += 0.25;
                            var elapsed = time - prevLeftTrackPadTime;
                            if (elapsed < 200)
                                args.Flags = 30; // double tap
                            prevLeftTrackPadTime = time;
                        }
                        else if (args.Status == CursorEvent.EventType.UP)
                            LeftTrackpad.Opacity -= 0.25;
                    }
                    break;

                case TouchTarget.TrackpadRight:
                    {
                        if (args.Status == CursorEvent.EventType.DOWN)
                        {
                            RightTrackpad.Opacity += 0.25;
                            var elapsed = time - prevRightTrackPadTime;
                            if (elapsed < 200)
                                args.Flags = 30; // double tap
                            prevRightTrackPadTime = time;
                        }
                        else if (args.Status == CursorEvent.EventType.UP)
                            RightTrackpad.Opacity -= 0.25;

                        normalizedX += 0.5d;
                    }
                    break;
            }

            this.pipeClient.SendMessage(new PipeClientCursor
            {
                action = args.Status == CursorEvent.EventType.DOWN ? CursorAction.CursorDown : args.Status == CursorEvent.EventType.UP ? CursorAction.CursorUp : CursorAction.CursorMove,
                x = normalizedX,
                y = normalizedY,
                button = Button,
                flags = args.Flags
            });
        }

        public void UpdateController(ControllerEx controllerEx)
        {
            this.controllerEx = controllerEx;
        }

        public void ControllerTriggerClicked()
        {
            controllerTriggerButtons = 0;
            ControllerTriggerListening = true;
        }

        public void TrackpadsTriggerClicked()
        {
            trackpadTriggerButtons = 0;
            TrackpadsTriggerListening = true;
        }

        public void UpdateControllerVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                Visibility visibility = Visibility.Visible;
                switch (VirtualController.Visibility)
                {
                    case Visibility.Visible:
                        visibility = Visibility.Hidden;
                        break;
                    case Visibility.Hidden:
                        visibility = Visibility.Visible;
                        break;
                }
                VirtualController.Visibility = visibility;
            });
        }

        public void UpdateTrackpadsVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                Visibility visibility = Visibility.Visible;
                switch (VirtualTrackpads.Visibility)
                {
                    case Visibility.Visible:
                        visibility = Visibility.Hidden;
                        break;
                    case Visibility.Hidden:
                        visibility = Visibility.Visible;
                        break;
                }
                VirtualTrackpads.Visibility = visibility;
            });
        }

        private void UpdateVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                this.Visibility = (VirtualController.Visibility == Visibility.Visible || VirtualTrackpads.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Hidden;
            });
            pipeClient.SendMessage(new PipeOverlay((int)VirtualController.Visibility));
        }

        #region ModelVisual3D
        private RotateTransform3D DeviceRotateTransform;
        private RotateTransform3D DeviceRotateTransformFaceCameraX;
        private RotateTransform3D DeviceRotateTransformFaceCameraY;
        private RotateTransform3D DeviceRotateTransformFaceCameraZ;
        private RotateTransform3D LeftJoystickRotateTransform;
        private RotateTransform3D RightJoystickRotateTransform;
        private RotateTransform3D TransformTriggerPositionLeft;
        private RotateTransform3D TransformTriggerPositionRight;

        private int m_ModelVisualUpdate;
        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SENSOR:
                    PipeSensor sensor = (PipeSensor)message;

                    switch (sensor.type)
                    {
                        case SensorType.Quaternion:
                            q_w = sensor.q_w;
                            q_x = sensor.q_x;
                            q_y = sensor.q_y;
                            q_z = sensor.q_z;

                            x = sensor.x;
                            y = sensor.y;
                            z = sensor.z;
                            break;
                    }
                    break;
            }
        }

        private Gamepad prevGamepad;
        private GeometryModel3D model = null;
        private void UpdateReport(object? sender, EventArgs e)
        {
            // get current gamepad state
            if (controllerEx != null && controllerEx.IsConnected())
            {
                GamepadState = controllerEx.GetState();
                Gamepad = GamepadState.Gamepad;
            }

            // update model based on Gamepad state
            if (prevGamepad.GetHashCode() != Gamepad.GetHashCode())
            {
                // Handle controller trigger(s)
                if (!ControllerTriggerListening && Gamepad.Buttons.HasFlag(controllerTriggerButtons))
                {
                    if (!controllerTriggered)
                    {
                        UpdateControllerVisibility();
                        UpdateVisibility();
                        controllerTriggered = true;
                    }
                }
                else if (controllerTriggered)
                {
                    controllerTriggered = false;
                }

                // handle controller trigger(s) update
                if (ControllerTriggerListening)
                {
                    if (Gamepad.Buttons != 0)
                        controllerTriggerButtons |= Gamepad.Buttons;
                    else if (Gamepad.Buttons == 0 && controllerTriggerButtons != 0)
                    {
                        ControllerTriggerUpdated?.Invoke(controllerTriggerButtons);
                        ControllerTriggerListening = false;
                    }
                }

                // Handle trackpad trigger(s)
                if (!TrackpadsTriggerListening && Gamepad.Buttons.HasFlag(trackpadTriggerButtons))
                {
                    if (!trackpadTriggered)
                    {
                        UpdateTrackpadsVisibility();
                        UpdateVisibility();
                        trackpadTriggered = true;
                    }
                }
                else if (trackpadTriggered)
                {
                    trackpadTriggered = false;
                }

                // handle trackpad trigger(s) update
                if (TrackpadsTriggerListening)
                {
                    if (Gamepad.Buttons != 0)
                        trackpadTriggerButtons |= Gamepad.Buttons;
                    else if (Gamepad.Buttons == 0 && trackpadTriggerButtons != 0)
                    {
                        TrackpadsTriggerUpdated?.Invoke(trackpadTriggerButtons);
                        TrackpadsTriggerListening = false;
                    }
                }

                this.Dispatcher.Invoke(() =>
                {
                    GeometryModel3D model = null;
                    foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
                    {
                        if (!CurrentModel.ButtonMap.ContainsKey(button))
                            continue;

                        foreach (Model3DGroup modelgroup in CurrentModel.ButtonMap[button])
                        {
                            model = (GeometryModel3D)modelgroup.Children.FirstOrDefault();
                            model.Material = Gamepad.Buttons.HasFlag(button) ? CurrentModel.HighlightMaterials[modelgroup] : CurrentModel.DefaultMaterials[modelgroup];
                        }
                    }
                });
            }

            // update gamepad state
            prevGamepad = Gamepad;

            // update model
            UpdateModelVisual3D();

            // skip virtual controller update if hidden or collapsed
            if (VirtualController.Visibility != Visibility.Visible)
                return;

            this.Dispatcher.Invoke(() =>
            {
                // TODO update motor placeholders!
                // Motor Left
                model = CurrentModel.LeftMotor.Children[0] as GeometryModel3D;
                model.Material = MotorLeftPlaceholder > 0 ? CurrentModel.HighlightMaterials[CurrentModel.LeftMotor] : CurrentModel.DefaultMaterials[CurrentModel.LeftMotor];

                // Motor Right
                model = CurrentModel.RightMotor.Children[0] as GeometryModel3D;
                model.Material = MotorRightPlaceholder > 0 ? CurrentModel.HighlightMaterials[CurrentModel.RightMotor] : CurrentModel.DefaultMaterials[CurrentModel.RightMotor];

                // ShoulderLeftTrigger
                model = CurrentModel.LeftShoulderTrigger.Children[0] as GeometryModel3D;
                model.Material = Gamepad.LeftTrigger > 0 ? CurrentModel.HighlightMaterials[CurrentModel.LeftShoulderTrigger] : CurrentModel.DefaultMaterials[CurrentModel.LeftShoulderTrigger];

                TriggerAngleShoulderLeft = -1 * CurrentModel.TriggerMaxAngleDeg * (float)Gamepad.LeftTrigger / (float)byte.MaxValue;

                // ShoulderRightTrigger
                model = CurrentModel.RightShoulderTrigger.Children[0] as GeometryModel3D;
                model.Material = Gamepad.RightTrigger > 0 ? CurrentModel.HighlightMaterials[CurrentModel.RightShoulderTrigger] : CurrentModel.DefaultMaterials[CurrentModel.RightShoulderTrigger];

                TriggerAngleShoulderRight = -1 * CurrentModel.TriggerMaxAngleDeg * (float)Gamepad.RightTrigger / (float)byte.MaxValue;

                // JoystickLeftRing
                model = CurrentModel.LeftThumbRing.Children[0] as GeometryModel3D;
                if (Gamepad.LeftThumbX != 0 || Gamepad.LeftThumbY != 0)
                {
                    // Adjust color
                    model.Material = CurrentModel.HighlightMaterials[CurrentModel.LeftThumbRing];

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickLeft = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * (float)Gamepad.LeftThumbX / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * (float)Gamepad.LeftThumbY / (float)short.MaxValue;

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
                if (Gamepad.RightThumbX != 0 || Gamepad.RightThumbY != 0)
                {
                    model.Material = CurrentModel.HighlightMaterials[CurrentModel.RightThumbRing];

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickRight = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * (float)Gamepad.RightThumbX / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * (float)Gamepad.RightThumbY / (float)short.MaxValue;

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

        public Vector3D DesiredAngle = new Vector3D(0, 0, 0);
        private float q_w, q_x, q_y, q_z;
        private float x = 0.0f, y = 0.0f, z = 0.0f;

        private void UpdateModelVisual3D()
        {
            m_ModelVisualUpdate++;

            /* reduce CPU usage by drawing every x calls
            if (m_ModelVisualUpdate % 2 != 0)
                return; */

            this.Dispatcher.Invoke(() =>
            {
                Transform3DGroup Transform3DGroupModel = new Transform3DGroup();

                // Device transformation based on pose
                Quaternion DevicePose = new Quaternion(q_w, q_x, q_y, q_z);
                var Ax3DDevicePose = new QuaternionRotation3D(DevicePose);
                DeviceRotateTransform = new RotateTransform3D(Ax3DDevicePose);
                Transform3DGroupModel.Children.Add(DeviceRotateTransform);

                // Angles
                Vector3D DiffAngle = new Vector3D(0, 0, 0);

                // Determine diff angles
                DiffAngle.X = (InputUtils.rad2deg(x) - (float)FaceCameraObjectAlignment.X) - (float)DesiredAngle.X;
                DiffAngle.Y = (InputUtils.rad2deg(y) - (float)FaceCameraObjectAlignment.Y) - (float)DesiredAngle.Y;
                DiffAngle.Z = (InputUtils.rad2deg(z) - (float)FaceCameraObjectAlignment.Z) - (float)DesiredAngle.Z;

                // Handle wrap around at -180 +180 position which is horizontal for steering
                DiffAngle.Y = (y < 0.0) ? DiffAngle.Y += 180.0f : DiffAngle.Y -= 180.0f;

                // Correction amount for camera, increase slowly
                FaceCameraObjectAlignment += DiffAngle * 0.0005; // 0.0015 = ~90 degrees in 30 seconds

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

                // Transform mode with group
                ModelVisual3D.Content.Transform = Transform3DGroupModel;

                // Upward visibility rotation for shoulder buttons
                // Model angle to compensate for
                float ModelPoseXDeg = InputUtils.rad2deg(x) - (float)FaceCameraObjectAlignment.X;
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

                // Left shoulder buttons visibility rotation and trigger button angle
                Model3DGroup Placeholder = CurrentModel.ButtonMap[GamepadButtonFlags.LeftShoulder][0];

                UpwardVisibilityRotationShoulderButtons(ShoulderButtonsAngleDeg,
                                                        CurrentModel.UpwardVisibilityRotationAxisLeft,
                                                        CurrentModel.UpwardVisibilityRotationPointLeft,
                                                        TriggerAngleShoulderLeft,
                                                        CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter,
                                                        ref CurrentModel.LeftShoulderTrigger,
                                                        ref Placeholder
                                                        );

                CurrentModel.ButtonMap[GamepadButtonFlags.LeftShoulder][0] = Placeholder;

                // Right shoulder buttons visibility rotation and trigger button angle
                Placeholder = CurrentModel.ButtonMap[GamepadButtonFlags.RightShoulder][0];

                UpwardVisibilityRotationShoulderButtons(ShoulderButtonsAngleDeg,
                                                        CurrentModel.UpwardVisibilityRotationAxisRight,
                                                        CurrentModel.UpwardVisibilityRotationPointRight,
                                                        TriggerAngleShoulderRight,
                                                        CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter,
                                                        ref CurrentModel.RightShoulderTrigger,
                                                        ref Placeholder
                                                        );

                CurrentModel.ButtonMap[GamepadButtonFlags.RightShoulder][0] = Placeholder;

            });
        }
        #endregion
    }
}