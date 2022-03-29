using ControllerCommon;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static HandheldCompanion.OverlayHook;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using System.Windows.Interop;
using TouchEventSample;
using SharpDX.XInput;
using System.Timers;

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
        private HandheldDevice handheldDevice;

        private Point OverlayPosition;
        private Point LeftTrackPadPosition;
        private Point RightTrackPadPosition;

        // Model3D vars
        Model3DGroup HandHeld = new Model3DGroup();
        ModelImporter importer = new ModelImporter();

        Model3DGroup DPadDown;
        Model3DGroup DPadLeft;
        Model3DGroup DPadRight;
        Model3DGroup DPadUp;
        Model3DGroup FaceButtonA;
        Model3DGroup FaceButtonB;
        Model3DGroup FaceButtonX;
        Model3DGroup FaceButtonY;
        Model3DGroup JoystickLeftRing;
        Model3DGroup JoystickLeftStick;
        Model3DGroup JoystickRightRing;
        Model3DGroup JoystickRightStick;
        Model3DGroup MainBody;
        Model3DGroup Screen;
        Model3DGroup ShoulderLeftButton;
        Model3DGroup ShoulderLeftMiddle;
        Model3DGroup ShoulderLeftTrigger;
        Model3DGroup ShoulderRightButton;
        Model3DGroup ShoulderRightMiddle;
        Model3DGroup ShoulderRightTrigger;
        Model3DGroup WFBEsc;
        Model3DGroup WFBH;
        Model3DGroup WFBKB;
        Model3DGroup WFBMenu;
        Model3DGroup WFBRGB;
        Model3DGroup WFBTM;
        Model3DGroup WFBView;
        Model3DGroup WFBWin;

        // Default Materials
        DiffuseMaterial MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")));
        DiffuseMaterial MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0EFF0"))); 
        DiffuseMaterial MaterialHighlight = new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#76B9ED")));

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

        public Overlay(ILogger microsoftLogger, PipeClient pipeClient, HandheldDevice handheldDevice) : this()
        {
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;

            this.handheldDevice = handheldDevice;

            // load 3D model (should be moved to handheldDevice)
            DPadDown = importer.Load($"models/{handheldDevice.ModelName}/DPad-Down.obj");
            DPadLeft = importer.Load($"models/{handheldDevice.ModelName}/DPad-Left.obj");
            DPadRight = importer.Load($"models/{handheldDevice.ModelName}/DPad-Right.obj");
            DPadUp = importer.Load($"models/{handheldDevice.ModelName}/DPad-Up.obj");
            FaceButtonA = importer.Load($"models/{handheldDevice.ModelName}/FaceButton-A.obj");
            FaceButtonB = importer.Load($"models/{handheldDevice.ModelName}/FaceButton-B.obj");
            FaceButtonX = importer.Load($"models/{handheldDevice.ModelName}/FaceButton-X.obj");
            FaceButtonY = importer.Load($"models/{handheldDevice.ModelName}/FaceButton-Y.obj");
            JoystickLeftRing = importer.Load($"models/{handheldDevice.ModelName}/Joystick-Left-Ring.obj");
            JoystickLeftStick = importer.Load($"models/{handheldDevice.ModelName}/Joystick-Left-Stick.obj");
            JoystickRightRing = importer.Load($"models/{handheldDevice.ModelName}/Joystick-Right-Ring.obj");
            JoystickRightStick = importer.Load($"models/{handheldDevice.ModelName}/Joystick-Right-Stick.obj");
            MainBody = importer.Load($"models/{handheldDevice.ModelName}/MainBody.obj");
            Screen = importer.Load($"models/{handheldDevice.ModelName}/Screen.obj");
            ShoulderLeftButton = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Left-Button.obj");
            ShoulderLeftMiddle = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Left-Middle.obj");
            ShoulderLeftTrigger = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Left-Trigger.obj");
            ShoulderRightButton = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Right-Button.obj");
            ShoulderRightMiddle = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Right-Middle.obj");
            ShoulderRightTrigger = importer.Load($"models/{handheldDevice.ModelName}/Shoulder-Right-Trigger.obj");
            WFBEsc = importer.Load($"models/{handheldDevice.ModelName}/WFB-Esc.obj");
            WFBH = importer.Load($"models/{handheldDevice.ModelName}/WFB-H.obj");
            WFBKB = importer.Load($"models/{handheldDevice.ModelName}/WFB-KB.obj");
            WFBMenu = importer.Load($"models/{handheldDevice.ModelName}/WFB-Menu.obj");
            WFBRGB = importer.Load($"models/{handheldDevice.ModelName}/WFB-RGB.obj");
            WFBTM = importer.Load($"models/{handheldDevice.ModelName}/WFB-TM.obj");
            WFBView = importer.Load($"models/{handheldDevice.ModelName}/WFB-View.obj");
            WFBWin = importer.Load($"models/{handheldDevice.ModelName}/WFB-Win.obj");

            HandHeld.Children.Add(DPadDown);
            HandHeld.Children.Add(DPadLeft);
            HandHeld.Children.Add(DPadRight);
            HandHeld.Children.Add(DPadUp);
            HandHeld.Children.Add(FaceButtonA);
            HandHeld.Children.Add(FaceButtonB);
            HandHeld.Children.Add(FaceButtonX);
            HandHeld.Children.Add(FaceButtonY);
            HandHeld.Children.Add(JoystickLeftRing);
            HandHeld.Children.Add(JoystickLeftStick);
            HandHeld.Children.Add(JoystickRightRing);
            HandHeld.Children.Add(JoystickRightStick);
            HandHeld.Children.Add(MainBody);
            HandHeld.Children.Add(Screen);
            HandHeld.Children.Add(ShoulderLeftButton);
            HandHeld.Children.Add(ShoulderLeftMiddle);
            HandHeld.Children.Add(ShoulderLeftTrigger);
            HandHeld.Children.Add(ShoulderRightButton);
            HandHeld.Children.Add(ShoulderRightMiddle);
            HandHeld.Children.Add(ShoulderRightTrigger);
            HandHeld.Children.Add(WFBEsc);
            HandHeld.Children.Add(WFBH);
            HandHeld.Children.Add(WFBKB);
            HandHeld.Children.Add(WFBMenu);
            HandHeld.Children.Add(WFBRGB);
            HandHeld.Children.Add(WFBTM);
            HandHeld.Children.Add(WFBView);
            HandHeld.Children.Add(WFBWin);

            ModelVisual3D.Content = HandHeld;
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

            this.Dispatcher.Invoke(() =>
            {
                // DPad
                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                {
                    GeometryModel3D model = DPadUp.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = DPadUp.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                {
                    GeometryModel3D model = DPadRight.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = DPadRight.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                {
                    GeometryModel3D model = DPadDown.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = DPadDown.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                {
                    GeometryModel3D model = DPadLeft.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = DPadLeft.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                // Facebuttons
                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                {
                    GeometryModel3D model = FaceButtonA.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = FaceButtonA.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                {
                    GeometryModel3D model = FaceButtonB.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = FaceButtonB.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                {
                    GeometryModel3D model = FaceButtonX.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = FaceButtonX.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                {
                    GeometryModel3D model = FaceButtonY.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = FaceButtonY.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                // Start / Select
                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                {
                    GeometryModel3D model = WFBMenu.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = WFBMenu.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                {
                    GeometryModel3D model = WFBView.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = WFBView.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                // Shoulder
                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                {
                    GeometryModel3D model = ShoulderLeftButton.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = ShoulderLeftButton.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.LeftTrigger > 0)
                {
                    GeometryModel3D model = ShoulderLeftTrigger.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = ShoulderLeftTrigger.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                {
                    GeometryModel3D model = ShoulderRightButton.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = ShoulderRightButton.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.RightTrigger > 0)
                {
                    GeometryModel3D model = ShoulderRightTrigger.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = ShoulderRightTrigger.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                // Joysticks
                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                {
                    GeometryModel3D model = JoystickLeftStick.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = JoystickLeftStick.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.LeftThumbX != 0 || gamepad.LeftThumbY != 0)
                {
                    // Adjust color
                    GeometryModel3D model = JoystickLeftRing.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;



                    // Define rotation amount
                    float x = 30.0f * (float)gamepad.LeftThumbX / (float)short.MaxValue;
                    float y = 30.0f * (float)gamepad.LeftThumbY / (float)short.MaxValue;
                    var ax3dX = new AxisAngleRotation3D(new Vector3D(0, 0, 1), x);
                    LeftJoystickRotateTransform = new RotateTransform3D(ax3dX);

                    // TODO most likely define a transformation group here for both X and Y see:
                    // https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-apply-multiple-transformations-to-a-3-d-model?view=netframeworkdesktop-4.8

                    // Define rotation point
                    LeftJoystickRotateTransform.CenterX = -110.0;
                    LeftJoystickRotateTransform.CenterY = -12.0;
                    LeftJoystickRotateTransform.CenterZ = 24.0;

                    // Transform joystick
                    JoystickLeftRing.Transform = LeftJoystickRotateTransform;
                    JoystickLeftStick.Transform = LeftJoystickRotateTransform;
                }
                else
                {
                    GeometryModel3D model = JoystickLeftRing.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;

                    // Rotate to default
                    var ax3d = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
                    LeftJoystickRotateTransform = new RotateTransform3D(ax3d);

                    // TODO most likely define a transformation group here for both X and Y see:
                    // https://docs.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-apply-multiple-transformations-to-a-3-d-model?view=netframeworkdesktop-4.8

                    // Define rotation point
                    LeftJoystickRotateTransform.CenterX = 0.0;
                    LeftJoystickRotateTransform.CenterY = -12.0;
                    LeftJoystickRotateTransform.CenterZ = 24.0;

                    // Transform joystick
                    JoystickLeftRing.Transform = LeftJoystickRotateTransform;
                    JoystickLeftStick.Transform = LeftJoystickRotateTransform;
                }

                if (gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                {
                    GeometryModel3D model = JoystickRightStick.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = JoystickRightStick.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
                }

                if (gamepad.RightThumbX != 0 || gamepad.RightThumbY != 0)
                {
                    GeometryModel3D model = JoystickRightRing.Children[0] as GeometryModel3D;
                    model.Material = MaterialHighlight;
                }
                else
                {
                    GeometryModel3D model = JoystickRightRing.Children[0] as GeometryModel3D;
                    model.Material = MaterialPlasticBlack;
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

                    // Default colors for static models
                    GeometryModel3D PlaceholderModel = Screen.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = MainBody.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticWhite;

                    PlaceholderModel = WFBEsc.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = WFBH.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = WFBKB.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = WFBRGB.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = WFBTM.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = WFBWin.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = ShoulderLeftMiddle.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

                    PlaceholderModel = ShoulderRightMiddle.Children[0] as GeometryModel3D;
                    PlaceholderModel.Material = MaterialPlasticBlack;

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