using ControllerCommon;
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
        private HandheldModels ProductModel;

        private Point OverlayPosition;
        private Point LeftTrackPadPosition;
        private Point RightTrackPadPosition;

        private TouchSourceWinTouch touchsource;

        // Gamepad vars
        private Gamepad gamepad;
        private Timer gamepadTimer;

        public Overlay()
        {
            InitializeComponent();

            touchsource = new TouchSourceWinTouch(this);
            touchsource.Touch += Touchsource_Touch;

            this.SourceInitialized += Overlay_SourceInitialized;

            this.gamepadTimer = new Timer() { Enabled = false, AutoReset = false, Interval = 500 };
            this.gamepadTimer.Elapsed += gamepadTimer_Elapsed;
        }

        public Overlay(ILogger microsoftLogger, PipeClient pipeClient) : this()
        {
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
        }

        public void SetHandheldModel(HandheldModels ProductModel)
        {
            // do something
            this.ProductModel = ProductModel;
            ModelVisual3D.Content = ProductModel.model3DGroup;
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

        private void Touchsource_Touch(TouchSourceWinTouch.TouchArgs args, long time)
        {
            double X = args.LocationX - this.OverlayPosition.X;
            double Y = args.LocationY - this.OverlayPosition.Y;

            double CenterX = this.ActualWidth / 2;
            bool isLeft = X < CenterX;
            CursorButton Button;
            Point CurrentPoint = new Point(0, 0);

            switch (isLeft)
            {
                // left trackpad
                default:
                case true:
                    Button = CursorButton.TouchLeft;
                    CurrentPoint = LeftTrackPadPosition;
                    break;

                // right trackpad
                case false:
                    Button = CursorButton.TouchRight;
                    CurrentPoint = RightTrackPadPosition;
                    break;
            }

            // normalized
            var relativeX = Math.Clamp(args.LocationX - CurrentPoint.X, 0, LeftTrackpad.ActualWidth);
            var relativeY = Math.Clamp(args.LocationY - CurrentPoint.Y, 0, LeftTrackpad.ActualHeight);

            var normalizedX = (relativeX / LeftTrackpad.ActualWidth) / 2.0d;
            var normalizedY = relativeY / LeftTrackpad.ActualHeight;

            switch (isLeft)
            {
                // left trackpad
                default:
                case true:
                    break;

                // right trackpad
                case false:
                    normalizedX += 0.5d;
                    break;
            }

            Debug.WriteLine(string.Format($"Id:{args.Id}, Status:{args.Status}, Button:{Button}, Flags:{args.Flags}, PadRelative.X:{normalizedX}, PadRelative.Y:{normalizedY}"));

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
                            UpdateModelVisual3D(sensor.q_w, sensor.q_x, sensor.q_y, sensor.q_z);
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
        private bool isReleased = true;
        private void UpdateReport()
        {
            isTriggered = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb) && gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
            if (isTriggered && isReleased)
            {
                gamepadTimer.Stop();
                gamepadTimer.Start();
                isReleased = false;
            }

            if (this.Visibility != Visibility.Visible)
                return;

            this.Dispatcher.Invoke(() =>
            {
                GeometryModel3D model = null;
                foreach (GamepadButtonFlags button in Enum.GetValues(typeof(GamepadButtonFlags)))
                {
                    if (!ProductModel.ButtonMap.ContainsKey(button))
                        continue;

                    foreach (Model3DGroup modelgroup in ProductModel.ButtonMap[button])
                    {
                        model = (GeometryModel3D)modelgroup.Children.FirstOrDefault();
                        if (gamepad.Buttons.HasFlag(button))
                            model.Material = ProductModel.MaterialHighlight;
                        else
                            model.Material = ProductModel.MaterialPlasticBlack;
                    }
                }

                // ShoulderLeftTrigger
                model = ProductModel.LeftShoulderTrigger.Children[0] as GeometryModel3D;
                if (gamepad.LeftTrigger > 0)
                {
                    model.Material = ProductModel.MaterialHighlight;

                    // Define and compute
                    float Angle = -1 * ProductModel.TriggerMaxAngleDeg * (float)gamepad.LeftTrigger / (float)byte.MaxValue;

                    // Rotation
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, 7.27), Angle);
                    LeftTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftTriggerRotateTransform.CenterX = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.X;
                    LeftTriggerRotateTransform.CenterY = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Y;
                    LeftTriggerRotateTransform.CenterZ = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Z;

                    // Transform trigger
                    ProductModel.LeftShoulderTrigger.Transform = LeftTriggerRotateTransform;
                }
                else
                {
                    model.Material = ProductModel.MaterialPlasticBlack;

                    // Rotation reset
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, 7.27), 0);
                    LeftTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftTriggerRotateTransform.CenterX = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.X;
                    LeftTriggerRotateTransform.CenterY = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Y;
                    LeftTriggerRotateTransform.CenterZ = ProductModel.ShoulderTriggerRotationPointCenterLeftMillimeter.Z;

                    // Transform trigger
                    ProductModel.LeftShoulderTrigger.Transform = LeftTriggerRotateTransform;
                }

                // ShoulderRightTrigger
                model = ProductModel.RightShoulderTrigger.Children[0] as GeometryModel3D;
                if (gamepad.RightTrigger > 0)
                {
                    model.Material = ProductModel.MaterialHighlight;

                    // Define and compute
                    float Angle = -1 * ProductModel.TriggerMaxAngleDeg * (float)gamepad.RightTrigger / (float)byte.MaxValue;

                    // Rotation
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, -7.27), Angle);
                    RightTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightTriggerRotateTransform.CenterX = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.X;
                    RightTriggerRotateTransform.CenterY = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.Y;
                    RightTriggerRotateTransform.CenterZ = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.Z;

                    // Transform trigger
                    ProductModel.RightShoulderTrigger.Transform = RightTriggerRotateTransform;
                }
                else
                {
                    model.Material = ProductModel.MaterialPlasticBlack;

                    // Rotation reset
                    var ax3d = new AxisAngleRotation3D(new Vector3D(26.915, 0, -7.27), 0);
                    RightTriggerRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightTriggerRotateTransform.CenterX = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.X;
                    RightTriggerRotateTransform.CenterY = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.Y;
                    RightTriggerRotateTransform.CenterZ = ProductModel.ShoulderTriggerRotationPointCenterRightMillimeter.Z;

                    // Transform trigger
                    ProductModel.RightShoulderTrigger.Transform = RightTriggerRotateTransform;
                }

                // JoystickLeftRing
                model = ProductModel.LeftThumbRing.Children[0] as GeometryModel3D;
                if (gamepad.LeftThumbX != 0 || gamepad.LeftThumbY != 0)
                {
                    // Adjust color
                    model.Material = ProductModel.MaterialHighlight;

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickLeft = new Transform3DGroup();
                    float x = ProductModel.JoystickMaxAngleDeg * (float)gamepad.LeftThumbX / (float)short.MaxValue;
                    float y = -1 * ProductModel.JoystickMaxAngleDeg * (float)gamepad.LeftThumbY / (float)short.MaxValue;

                    // Rotation X
                    var ax3d = new AxisAngleRotation3D(new Vector3D(0, 0, 1), x);
                    LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterLeftMillimeter.X;
                    LeftJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterLeftMillimeter.Y;
                    LeftJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterLeftMillimeter.Z;

                    Transform3DGroupJoystickLeft.Children.Add(LeftJoystickRotateTransform);

                    // Rotation Y
                    ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), y);
                    LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterLeftMillimeter.X;
                    LeftJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterLeftMillimeter.Y;
                    LeftJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterLeftMillimeter.Z;

                    Transform3DGroupJoystickLeft.Children.Add(LeftJoystickRotateTransform);

                    // Transform joystick group
                    ProductModel.LeftThumbRing.Transform = ProductModel.LeftThumb.Transform = Transform3DGroupJoystickLeft;
                }
                else
                {
                    // Default material color, no highlight
                    model.Material = ProductModel.MaterialPlasticBlack;

                    // Define and compute, back to default position
                    var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                    LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    LeftJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterLeftMillimeter.X;
                    LeftJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterLeftMillimeter.Y;
                    LeftJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterLeftMillimeter.Z;

                    // Transform joystick
                    ProductModel.LeftThumbRing.Transform = ProductModel.LeftThumb.Transform = LeftJoystickRotateTransform;
                }

                // JoystickRightRing
                model = ProductModel.RightThumbRing.Children[0] as GeometryModel3D;
                if (gamepad.RightThumbX != 0 || gamepad.RightThumbY != 0)
                {
                    model.Material = ProductModel.MaterialHighlight;

                    // Define and compute
                    Transform3DGroup Transform3DGroupJoystickRight = new Transform3DGroup();
                    float x = ProductModel.JoystickMaxAngleDeg * (float)gamepad.RightThumbX / (float)short.MaxValue;
                    float y = -1 * ProductModel.JoystickMaxAngleDeg * (float)gamepad.RightThumbY / (float)short.MaxValue;

                    // Rotation X
                    var ax3d = new AxisAngleRotation3D(new Vector3D(0, 0, 1), x);
                    RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterRightMillimeter.X;
                    RightJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterRightMillimeter.Y;
                    RightJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterRightMillimeter.Z;

                    Transform3DGroupJoystickRight.Children.Add(RightJoystickRotateTransform);

                    // Rotation Y
                    ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), y);
                    RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterRightMillimeter.X;
                    RightJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterRightMillimeter.Y;
                    RightJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterRightMillimeter.Z;

                    Transform3DGroupJoystickRight.Children.Add(RightJoystickRotateTransform);

                    // Transform joystick group
                    ProductModel.RightThumbRing.Transform = ProductModel.RightThumb.Transform = Transform3DGroupJoystickRight;

                }
                else
                {
                    model.Material = ProductModel.MaterialPlasticBlack;

                    // Define and compute, back to default position
                    var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                    RightJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // Define rotation point
                    RightJoystickRotateTransform.CenterX = ProductModel.JoystickRotationPointCenterRightMillimeter.X;
                    RightJoystickRotateTransform.CenterY = ProductModel.JoystickRotationPointCenterRightMillimeter.Y;
                    RightJoystickRotateTransform.CenterZ = ProductModel.JoystickRotationPointCenterRightMillimeter.Z;

                    // Transform joystick
                    ProductModel.RightThumbRing.Transform = ProductModel.RightThumb.Transform = RightJoystickRotateTransform;
                }
            });
        }

        private void gamepadTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var isTriggered = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb) && gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
            if (isTriggered && this.isTriggered)
            {
                this.Dispatcher.Invoke(() =>
                {
                    switch (this.Visibility)
                    {
                        case Visibility.Visible:
                            this.Visibility = Visibility.Collapsed;
                            break;
                        case Visibility.Hidden:
                        case Visibility.Collapsed:
                            this.Visibility = Visibility.Visible;
                            break;
                    }

                    pipeClient.SendMessage(new PipeOverlay((int)this.Visibility));
                });
            }

            isReleased = true;
        }

        private void UpdateModelVisual3D(float q_w, float q_x, float q_y, float q_z)
        {
            m_ModelVisualUpdate++;

            // reduce CPU usage by drawing every x calls
            if (m_ModelVisualUpdate % 2 != 0)
                return;

            this.Dispatcher.Invoke(() =>
            {
                Quaternion endQuaternion = new Quaternion(q_w, q_x, q_y, q_z);
                var ax3dalt = new QuaternionRotation3D(endQuaternion);

                DeviceRotateTransform = new RotateTransform3D(ax3dalt);
                ModelVisual3D.Content.Transform = DeviceRotateTransform;
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
                WinEventDelegate = new WinEventDelegate(WinEventCallback);
                GCSafetyHandle = GCHandle.Alloc(WinEventDelegate);
                targetProc = Process.GetProcessById((int)processid);

                if (targetProc != null)
                {
                    hWnd = targetProc.MainWindowHandle;

                    if (hWnd != IntPtr.Zero)
                    {
                        uint targetThreadId = GetWindowThread(hWnd);

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
            catch (Exception ex) { }
        }
        #endregion
    }
}