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

        private ILogger microsoftLogger;
        private PipeClient pipeClient;
        private HandheldDevice handheldDevice;

        // Model3D vars
        Model3DGroup HandHeld = new Model3DGroup();
        ModelImporter importer = new ModelImporter();
        Model3DGroup MainBody;
        Model3DGroup Screen;
        Model3DGroup ShoulderButtonLeft;
        Model3DGroup ShoulderButtonRight;

        private TouchSourceWinTouch touchsource;

        // Gamepad vars
        private Gamepad gamepad;
        private Timer gamepadTimer;

        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public Overlay()
        {
            InitializeComponent();

            touchsource = new TouchSourceWinTouch(this);
            touchsource.Touch += Touchsource_Touch;

            this.gamepadTimer = new Timer() { Enabled = false, AutoReset = false, Interval = 1000 };
            this.gamepadTimer.Elapsed += gamepadTimer_Elapsed;
        }

        private void Touchsource_Touch(TouchSourceWinTouch.TouchArgs args)
        {
            int X = (int)args.LocationX;
            int Y = (int)args.LocationY;

            switch (args.Status)
            {
                case CursorEvent.EventType.DOWN:
                    break;

                case CursorEvent.EventType.MOVE:
                    break;

                case CursorEvent.EventType.UP:
                    break;
            }
        }

        public Overlay(ILogger microsoftLogger, PipeClient pipeClient, HandheldDevice handheldDevice) : this()
        {
            this.microsoftLogger = microsoftLogger;

            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;

            this.handheldDevice = handheldDevice;

            // load 3D model
            MainBody = importer.Load($"models/{handheldDevice.ModelName}/MainBody.obj");
            Screen = importer.Load($"models/{handheldDevice.ModelName}/Screen.obj");
            ShoulderButtonLeft = importer.Load($"models/{handheldDevice.ModelName}/ShoulderButtonLeft.obj");
            ShoulderButtonRight = importer.Load($"models/{handheldDevice.ModelName}/ShoulderButtonRight.obj");

            HandHeld.Children.Add(MainBody);
            HandHeld.Children.Add(Screen);
            HandHeld.Children.Add(ShoulderButtonLeft);
            HandHeld.Children.Add(ShoulderButtonRight);

            ModelVisual3D.Content = HandHeld;
        }

        #region ModelVisual3D
        private RotateTransform3D myRotateTransform;
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

        private bool isTriggered;
        private void UpdateReport()
        {
            isTriggered = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb) && gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
            if (isTriggered)
            {
                gamepadTimer.Stop();
                gamepadTimer.Start();
            }
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

                    DiffuseMaterial material = new DiffuseMaterial(new SolidColorBrush(Colors.White));
                    GeometryModel3D model = Screen.Children[0] as GeometryModel3D;
                    model.Material = material;

                    pipeClient.SendMessage(new PipeOverlay((int)this.Visibility));
                });
            }
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

                myRotateTransform = new RotateTransform3D(ax3dalt);
                ModelVisual3D.Content.Transform = myRotateTransform;
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
            if (hWnd == this.hWnd &&
                eventType == NativeMethods.SWEH_Events.EVENT_OBJECT_LOCATIONCHANGE &&
                idObject == (NativeMethods.SWEH_ObjectId)NativeMethods.SWEH_CHILDID_SELF)
            {
                var rect = GetWindowRectangle(hWnd);
                this.Top = rect.Top;
                this.Left = rect.Left;
                this.Width = rect.Right - rect.Left;
                this.Height = rect.Bottom - rect.Top;
            }
        }

        public void HookInto(uint processid)
        {
            WinEventDelegate = new WinEventDelegate(WinEventCallback);
            GCSafetyHandle = GCHandle.Alloc(WinEventDelegate);
            targetProc = Process.GetProcessById((int)processid);

            try
            {
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

                        SetForegroundWindow(hWnd);
                    }
                }
            }
            catch (Exception ex) { }
        }
        #endregion
    }
}
