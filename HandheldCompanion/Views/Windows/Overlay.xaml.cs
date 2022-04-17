using ControllerCommon;
using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Media3D;
using TouchEventSample;
using static HandheldCompanion.OverlayHook;
using static TouchEventSample.TouchSourceWinTouch;
using GamepadButtonFlags = SharpDX.XInput.GamepadButtonFlags;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class Overlay : Window
    {
        private IntPtr hWnd;
        private IntPtr hWinEventHook;
        private Process targetProc = null;

        protected WinEventDelegate WinEventDelegate;
        static GCHandle GCSafetyHandle;
        private bool isHooked = true; // hack

        #region import
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        #endregion

        private ILogger microsoftLogger;
        private PipeClient pipeClient;

        private Model CurrentModel;
        private Model ProductModel;
        private Model VirtualModel;
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
        private Gamepad gamepad;

        private Vector3D FaceCameraObjectAlignment;
        private Quaternion FaceCameraObjectAlignmentQuat;

        public event ControllerTriggerUpdatedEventHandler ControllerTriggerUpdated;
        public delegate void ControllerTriggerUpdatedEventHandler(GamepadButtonFlags button);

        public event TrackpadsTriggerUpdatedEventHandler TrackpadsTriggerUpdated;
        public delegate void TrackpadsTriggerUpdatedEventHandler(GamepadButtonFlags button);

        // TODO Dummy variables, placeholder and for testing 
        short MotorLeftPlaceholder;
        short MotorRightPlaceholder;

        public Overlay()
        {
            InitializeComponent();

            // hook vars
            WinEventDelegate = new WinEventDelegate(WinEventCallback);
            GCSafetyHandle = GCHandle.Alloc(WinEventDelegate);

            // touch vars
            touchsource = new TouchSourceWinTouch(this);
            touchsource.Touch += Touchsource_Touch;

            this.SourceInitialized += Overlay_SourceInitialized;
        }

        public Overlay(ILogger microsoftLogger, PipeClient pipeClient) : this()
        {
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
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


            }

            ModelViewPort.ZoomExtents(); 
        }

        private void Overlay_SourceInitialized(object? sender, EventArgs e)
        {
            //Set the window style to noactivate.
            WindowInteropHelper helper = new WindowInteropHelper(this);
            SetWindowLong(helper.Handle, GWL_EXSTYLE,
                GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);

            this.LeftTrackPadPosition = LeftTrackpad.PointToScreen(new Point(0, 0));
            this.RightTrackPadPosition = RightTrackpad.PointToScreen(new Point(0, 0));
        }

        private void Touchsource_Touch(TouchArgs args, long time)
        {
            /* handle top screen swipe
            if (swipe != null && args.Id == swipe.Id)
            {
                if (args.Status == CursorEvent.EventType.MOVE)
                    if (args.LocationY - swipe.LocationY > 40) // hardcoded
                    {
                        swipe = null;
                        UpdateControllerVisibility();
                    }
                return;
            } */

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

        #region ModelVisual3D
        private RotateTransform3D DeviceRotateTransform;
        private RotateTransform3D DeviceRotateTransformFaceCameraX;
        private RotateTransform3D DeviceRotateTransformFaceCameraY;
        private RotateTransform3D DeviceRotateTransformFaceCameraZ;
        private RotateTransform3D LeftJoystickRotateTransform;
        private RotateTransform3D RightJoystickRotateTransform;
        private RotateTransform3D LeftTriggerRotateTransform;
        private RotateTransform3D RightTriggerRotateTransform;

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
                            // update ModelVisual3D
                            UpdateModelVisual3D(sensor.q_w, sensor.q_x, sensor.q_y, sensor.q_z, sensor.x, sensor.y, sensor.z);

                            // TODO remove, for testing only!
                            if (InputUtils.rad2deg(sensor.y) < 165.0 && InputUtils.rad2deg(sensor.y) > 0)
                            {
                                MotorLeftPlaceholder = (short)1;
                            }
                            else { MotorLeftPlaceholder = (short)0; }

                            // TODO remove, for testing only!
                            if (InputUtils.rad2deg(sensor.y) > -165.0 && InputUtils.rad2deg(sensor.y) < 0)
                            {
                                MotorRightPlaceholder = (short)1;
                            }
                            else { MotorRightPlaceholder = (short)0; }

                            break;
                    }
                    break;

                case PipeCode.SERVER_GAMEPAD:
                    PipeGamepad pipeGamepad = (PipeGamepad)message;
                    gamepad = pipeGamepad.ToGamepad();
                    UpdateReport();
                    break;
            }
        }

        private bool isTriggered = false;
        public GamepadButtonFlags mainTrigger = GamepadButtonFlags.Back;
        public GamepadButtonFlags controllerTrigger = GamepadButtonFlags.DPadUp;
        public GamepadButtonFlags trackpadTrigger = GamepadButtonFlags.DPadDown;

        private void UpdateReport()
        {
            // Handle triggers
            if ((gamepad.Buttons & mainTrigger) != 0)
            {
                if (!isTriggered)
                    isTriggered = true;
                else
                {
                    if (gamepad.Buttons.HasFlag(controllerTrigger))
                        UpdateControllerVisibility();
                    if (gamepad.Buttons.HasFlag(trackpadTrigger))
                        UpdateTrackpadsVisibility();

                    UpdateVisibility();
                }
            }
            else if (isTriggered)
                isTriggered = false;

            // handle triggers update
            if (ControllerTriggerListening)
            {
                ControllerTriggerUpdated?.Invoke(gamepad.Buttons);
                ControllerTriggerListening = false;
            }

            if (TrackpadsTriggerListening)
            {
                TrackpadsTriggerUpdated?.Invoke(gamepad.Buttons);
                TrackpadsTriggerListening = false;
            }

            if (VirtualController.Visibility != Visibility.Visible)
                return;

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
                        if (gamepad.Buttons.HasFlag(button))
                            model.Material = CurrentModel.MaterialHighlight;
                        else
                            model.Material = CurrentModel.MaterialPlasticBlack;
                    }
                }

                // TODO update motor placeholders!
                // Motor Left
                model = CurrentModel.LeftMotor.Children[0] as GeometryModel3D;
                if (MotorLeftPlaceholder > 0)
                {
                    model.Material = CurrentModel.MaterialHighlight;
                }
                else
                {
                    model.Material = CurrentModel.MaterialPlasticWhite;
                }

                // Motor Right
                model = CurrentModel.RightMotor.Children[0] as GeometryModel3D;
                if (MotorRightPlaceholder > 0)
                {
                    model.Material = CurrentModel.MaterialHighlight;
                }
                else
                {
                    model.Material = CurrentModel.MaterialPlasticWhite;
                }

                // ShoulderLeftTrigger
                model = CurrentModel.LeftShoulderTrigger.Children[0] as GeometryModel3D;
                if (gamepad.LeftTrigger > 0)
                {
                    model.Material = CurrentModel.MaterialHighlight;

                    // Define and compute
                    float Angle = -1 * CurrentModel.TriggerMaxAngleDeg * (float)gamepad.LeftTrigger / (float)byte.MaxValue;

                    // Rotation
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, 7.27), Angle);
                    LeftTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftTriggerRotateTransform.CenterX = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.X;
                    LeftTriggerRotateTransform.CenterY = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Y;
                    LeftTriggerRotateTransform.CenterZ = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Z;

                    // Transform trigger
                    CurrentModel.LeftShoulderTrigger.Transform = LeftTriggerRotateTransform;
                }
                else
                {
                    model.Material = CurrentModel.MaterialPlasticBlack;

                    // Rotation reset
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, 7.27), 0);
                    LeftTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftTriggerRotateTransform.CenterX = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.X;
                    LeftTriggerRotateTransform.CenterY = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Y;
                    LeftTriggerRotateTransform.CenterZ = CurrentModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Z;

                    // Transform trigger
                    CurrentModel.LeftShoulderTrigger.Transform = LeftTriggerRotateTransform;
                }

                // ShoulderRightTrigger
                model = CurrentModel.RightShoulderTrigger.Children[0] as GeometryModel3D;
                if (gamepad.RightTrigger > 0)
                {
                    model.Material = CurrentModel.MaterialHighlight;

                    // Define and compute
                    float Angle = -1 * CurrentModel.TriggerMaxAngleDeg * (float)gamepad.RightTrigger / (float)byte.MaxValue;

                    // Rotation
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, -7.27), Angle);
                    RightTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightTriggerRotateTransform.CenterX = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.X;
                    RightTriggerRotateTransform.CenterY = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.Y;
                    RightTriggerRotateTransform.CenterZ = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.Z;

                    // Transform trigger
                    CurrentModel.RightShoulderTrigger.Transform = RightTriggerRotateTransform;
                }
                else
                {
                    model.Material = CurrentModel.MaterialPlasticBlack;

                    // Rotation reset
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, -7.27), 0);
                    RightTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightTriggerRotateTransform.CenterX = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.X;
                    RightTriggerRotateTransform.CenterY = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.Y;
                    RightTriggerRotateTransform.CenterZ = CurrentModel.ShoulderTriggerRotationPointCenterRightMillimeter.Z;

                    // Transform trigger
                    CurrentModel.RightShoulderTrigger.Transform = RightTriggerRotateTransform;
                }

                // JoystickLeftRing
                model = CurrentModel.LeftThumbRing.Children[0] as GeometryModel3D;
                if (gamepad.LeftThumbX != 0 || gamepad.LeftThumbY != 0)
                {
                    // Adjust color
                    model.Material = CurrentModel.MaterialHighlight;

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickLeft = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * (float)gamepad.LeftThumbX / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * (float)gamepad.LeftThumbY / (float)short.MaxValue;

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
                    model.Material = CurrentModel.MaterialPlasticBlack;

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
                if (gamepad.RightThumbX != 0 || gamepad.RightThumbY != 0)
                {
                    model.Material = CurrentModel.MaterialHighlight;

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickRight = new Transform3DGroup();
                    float x = CurrentModel.JoystickMaxAngleDeg * (float)gamepad.RightThumbX / (float)short.MaxValue;
                    float y = -1 * CurrentModel.JoystickMaxAngleDeg * (float)gamepad.RightThumbY / (float)short.MaxValue;

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
                    model.Material = CurrentModel.MaterialPlasticBlack;

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

        private bool ControllerTriggerListening = false;
        public void ControllerTriggerClicked()
        {
            ControllerTriggerListening = true;
        }

        private bool TrackpadsTriggerListening = false;
        public void TrackpadsTriggerClicked()
        {
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

        private void UpdateModelVisual3D(float q_w, float q_x, float q_y, float q_z, float x, float y, float z)
        {
            m_ModelVisualUpdate++;

            // reduce CPU usage by drawing every x calls
            if (m_ModelVisualUpdate % 2 != 0)
                return;

            this.Dispatcher.Invoke(() =>
            {
                Transform3DGroup Transform3DGroupModel = new Transform3DGroup();

                // Device transformation based on pose
                Quaternion DevicePose = new Quaternion(q_w, q_x, q_y, q_z);
                var Ax3DDevicePose = new QuaternionRotation3D(DevicePose);
                DeviceRotateTransform = new RotateTransform3D(Ax3DDevicePose);
                Transform3DGroupModel.Children.Add(DeviceRotateTransform);

                // Angles
                Vector3D DesiredAngle = new Vector3D(0, 0, 0);
                Vector3D DiffAngle = new Vector3D(0, 0, 0);

                // Determine diff angles
                DiffAngle.X = (InputUtils.rad2deg(x) - (float)FaceCameraObjectAlignment.X) - (float)DesiredAngle.X;
                DiffAngle.Y = (InputUtils.rad2deg(y) - (float)FaceCameraObjectAlignment.Y) - (float)DesiredAngle.Y;
                DiffAngle.Z = (InputUtils.rad2deg(z) - (float)FaceCameraObjectAlignment.Z) - (float)DesiredAngle.Z;

                // Handle wrap around at -180 +180 position which is horizontal for steering
                DiffAngle.Y = (y < 0.0) ? DiffAngle.Y += 180.0f : DiffAngle.Y -= 180.0f;

                // Correction amount for camera, increase slowly
                FaceCameraObjectAlignment += DiffAngle * 0.0015; // 0.0015 = ~90 degrees in 30 seconds

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
            });
        }
        #endregion

        #region Hook
        protected void WinEventCallback(
            IntPtr hWinEventHook,
            NativeMethods.SWEH_Events eventType,
            IntPtr hWnd,
            NativeMethods.SWEH_ObjectId idObject,
            long idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (hWnd == this.hWnd &&
                eventType == NativeMethods.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE &&
                idObject == (NativeMethods.SWEH_ObjectId)NativeMethods.SWEH_CHILDID_SELF)
                {
                    var rect = GetWindowRectangle(hWnd);
                    this.Top = rect.Top;
                    this.Left = rect.Left;
                    this.Width = rect.Right - rect.Left;
                    this.Height = rect.Bottom - rect.Top;

                    this.OverlayPosition = new Point(rect.Left, rect.Top);
                    this.LeftTrackPadPosition = LeftTrackpad.PointToScreen(new Point(0, 0));
                    this.RightTrackPadPosition = RightTrackpad.PointToScreen(new Point(0, 0));
                }
            }
            catch (Exception ex) { }
        }

        public void HookInto(uint processid)
        {
            try
            {
                targetProc = Process.GetProcessById((int)processid);

                if (targetProc != null)
                {
                    hWnd = targetProc.MainWindowHandle;

                    if (hWnd != IntPtr.Zero)
                    {
                        uint targetThreadId = GetWindowThread(hWnd);
                        isHooked = true;

                        hWinEventHook = WinEventHookOne(
                            NativeMethods.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE,
                            WinEventDelegate, (uint)targetProc.Id, targetThreadId);

                        var rect = GetWindowRectangle(hWnd);

                        this.Top = rect.Top;
                        this.Left = rect.Left;
                        this.Width = rect.Right - rect.Left;
                        this.Height = rect.Bottom - rect.Top;

                        this.OverlayPosition = new Point(rect.Left, rect.Top);
                    }
                }
            }
            catch (Exception ex) { UnHook(); }
        }

        public void UnHook()
        {
            UpdateControllerVisibility();

            targetProc = null;
            hWnd = IntPtr.Zero;
            isHooked = false;
        }
        #endregion
    }
}