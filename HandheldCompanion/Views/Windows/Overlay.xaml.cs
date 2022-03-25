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

        private MainWindow mainWindow;
        private ILogger microsoftLogger;
        private PipeClient pipeClient;

        public Overlay()
        {
            InitializeComponent();
        }

        public Overlay(MainWindow mainWindow, ILogger microsoftLogger, PipeClient pipeClient) : this()
        {
            this.mainWindow = mainWindow;
            this.microsoftLogger = microsoftLogger;
            this.pipeClient = pipeClient;
            this.pipeClient.ServerMessage += OnServerMessage;
        }

        private void OnServerMessage(object sender, PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_SENSOR:
                    PipeSensor sensor = (PipeSensor)message;

                    switch (sensor.type)
                    {
                        case SensorType.Quaternion:
                            // do something
                            this.Dispatcher.Invoke(() =>
                            {
                                var ax3d = new AxisAngleRotation3D(new Vector3D(sensor.x, sensor.y, sensor.z), 1);
                                Quaternion endQuaternion = new Quaternion(sensor.q_x, sensor.q_y, sensor.q_z, sensor.q_w);
                                var ax3dalt = new QuaternionRotation3D(endQuaternion);

                                
                                RotateTransform3D myRotateTransform = new RotateTransform3D(ax3dalt);
                                myRotateTransform.CenterX = 0.5;
                                myRotateTransform.CenterY = 0.5;
                                myRotateTransform.CenterZ = 0.5;

                                MyModel.Transform = myRotateTransform; 
                            });


                            break;
                    }
                    break;
            }
        }

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
                Debug.WriteLine($"Top:{Top}, Left:{Left}");
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
                        // Of course, set the Form.StartPosition to Manual
                        this.Top = rect.Top;
                        this.Left = rect.Left;
                        this.Dispatcher.Invoke(() =>
                        {
                            this.Show();
                        });
                    }
                }
            }
            catch (Exception ex) { }
        }
    }
}
