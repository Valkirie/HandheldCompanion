using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TouchEventSample
{
    public class CursorEvent
    {
        public Cursor cursor
        { get; private set; }

        public EventType type
        { get; private set; }

        public CursorEvent(Cursor c, EventType t)
        {
            cursor = c;
            type = t;
        }

        public enum EventType
        { DOWN, MOVE, UP };
    }

    /// <summary>
    /// <para>A TouchSource listening to Windows VW_TOUCH event.</para>
    /// <para> The touch event are not sent for each message received but at regular interval.
    /// If an event is sent for each message received, it is difficult to calculate velocity of the cursor.
    /// Cursor Down and Up event are sent immediatly</para>
    /// </summary>
    public class TouchSourceWinTouch
    {
        #region Arguments

        public class TouchArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// Gets the x size of the contact area in pixels.
            /// </summary>
            public Double ContactX
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the y size of the contact area in pixels.
            /// </summary>
            public Double ContactY
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the total number of current touch points.
            /// </summary>
            public Int32 Count
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the given flags.
            /// </summary>
            public Int32 Flags
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the contact ID.
            /// </summary>
            public Int32 Id
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the touch x client coordinate in pixels.
            /// </summary>
            public Double LocationX
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the touch y client coordinate in pixels.
            /// </summary>
            public Double LocationY
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the mask which fields in the structure are valid.
            /// </summary>
            public Int32 Mask
            {
                get;
                internal set;
            }

            public CursorEvent.EventType Status
            {
                get;
                internal set;
            }

            /// <summary>
            /// Gets the touch event time.
            /// </summary>
            public long Time
            {
                get;
                internal set;
            }

            #endregion Properties
        }

        #endregion Arguments
        private Dictionary<int, uint> _idMapping;
        private bool _touchLost = false;
        private Window win;
        //private Dictionary<int, TouchArgs> pendingCursor;

        //private DispatcherTimer _Timer;
        public event EventHandler TouchLost;

        /// <summary>
        ///
        /// </summary>
        /// <param name="w"></param>
        public TouchSourceWinTouch(Window w)
        {
            win = w;
            _idMapping = new Dictionary<int, uint>(100);

            DisableWPFTabletSupport();

            //win.SourceInitialized += OnSourceInitialized;
            if (win.IsLoaded)
                OnSourceInitialized(this, new EventArgs());
            else
                win.Loaded += OnSourceInitialized;
            //win.SourceInitialized += OnSourceInitialized;
        }

        #region Disable WPF tablet support

        /// <summary>
        /// Code from https://social.msdn.microsoft.com/Forums/en-US/33828e1b-224a-4b73-86b5-9af949f07508/installing-net-452-breaks-microsofts-recommended-disablewpftabletsupport-method-for-disabling?forum=wpf
        /// </summary>
        private static void DisableWPFTabletSupport()
        {
            // Get a collection of the tablet devices for this window.
            var devices = Tablet.TabletDevices;

            if (devices.Count > 0)
            {
                // Get the Type of InputManager.
                var inputManagerType = typeof(InputManager);

                // Call the StylusLogic method on the InputManager.Current instance.
                var stylusLogic = inputManagerType.InvokeMember("StylusLogic",
                            BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                            null, InputManager.Current, null);

                if (stylusLogic != null)
                {
                    //  Get the type of the device class.
                    var devicesType = devices.GetType();

                    // Loop until there are no more devices to remove.
                    var count = devices.Count + 1;

                    while (devices.Count > 0)
                    {
                        // Remove the first tablet device in the devices collection.
                        devicesType.InvokeMember("HandleTabletRemoved", BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.NonPublic, null, devices, new object[] { (uint)0 });

                        count--;

                        if (devices.Count != count)
                        {
                            throw new Win32Exception("Unable to remove real-time stylus support.");
                        }
                    }
                }
            }
        }

        #endregion Disable WPF tablet support

        #region Enable touch

        private static readonly Int32 WM_TOUCH = 0x0240;
        private static bool _touchRegistered = false;
        private static IntPtr notificationHandle;
        private Dispatcher _backgroundDispatcher;
        private ConcurrentQueue<TouchArgs> RawCursor = new ConcurrentQueue<TouchArgs>();

        public static bool IsTouchEnabled()
        {
            var value = (SM_DIGITIZER_FLAG)GetSystemMetrics(SM_DIGITIZER);
            return value.HasFlag(SM_DIGITIZER_FLAG.NID_EXTERNAL_TOUCH)
                || value.HasFlag(SM_DIGITIZER_FLAG.NID_INTEGRATED_TOUCH)
                || value.HasFlag(SM_DIGITIZER_FLAG.NID_EXTERNAL_PEN)
                || value.HasFlag(SM_DIGITIZER_FLAG.NID_INTEGRATED_PEN);
        }

        public static void RegisterUsbDeviceNotification(IntPtr windowHandle)
        {
            DevBroadcastDeviceinterface dbi = new DevBroadcastDeviceinterface
            {
                DeviceType = DbtDevtypDeviceinterface,
                Reserved = 0,
                ClassGuid = GuidDevinterfaceUSBDevice,
                Name = 0
            };

            dbi.Size = Marshal.SizeOf(dbi);
            IntPtr buffer = Marshal.AllocHGlobal(dbi.Size);
            Marshal.StructureToPtr(dbi, buffer, true);

            notificationHandle = RegisterDeviceNotification(windowHandle, buffer, 0);
        }

        protected void OnSourceInitialized(object o, EventArgs e)
        {
            var source = PresentationSource.FromVisual(win) as HwndSource;

            RegisterUsbDeviceNotification(source.Handle);

            RegisterTouchEvent(source.Handle);

            //setup windows hook here
            source.AddHook(WndProc);
        }

        private void OnTouchNotAvailable()
        {
            if (_touchLost)
                return;
            _touchLost = true;
            TouchLost?.Invoke(this, EventArgs.Empty);
        }

        public event TouchEventHandler Touch;
        public delegate void TouchEventHandler(TouchArgs args);

        private void ProcessTouchArg(TouchArgs e, long time)
        {
            Touch?.Invoke(e);
            switch (e.Status)
            {
                case CursorEvent.EventType.DOWN:
                    {
                        Debug.WriteLine(string.Format(" id {0} event {1}", e.Id, "down"));
                    }
                    break;

                case CursorEvent.EventType.MOVE:
                    {
                        Debug.WriteLine(string.Format(" id {0} event {1}", e.Id, "move"));
                    }
                    break;

                case CursorEvent.EventType.UP:
                    {
                        Debug.WriteLine(string.Format(" id {0} event {1}", e.Id, "up"));
                    }
                    break;
            }
        }

        private bool RegisterTouchEvent(IntPtr handle)
        {
            if (!IsTouchEnabled())
            {
                Debug.WriteLine("no Touch device available");
                if (_touchRegistered)
                {
                    CloseTouchInputHandle(handle);
                    _touchRegistered = false;
                    Debug.WriteLine("unregister touch event");
                }
                OnTouchNotAvailable();
                return false;
            }
            if (_touchRegistered)
                return true;

            if (!RegisterTouchWindow(handle, RegisterTouchFlags.TWF_WANTPALM))
            {
                var err = Marshal.GetLastWin32Error();
                Debug.WriteLine("cant register touch window. error " + err);
                return false;
            }
            else
                Debug.WriteLine("Win Touch source initialysed");
            _touchRegistered = true;

            return true;
        }

        //[System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        private IntPtr WndProc(IntPtr hwnd, Int32 msg, IntPtr wParam, IntPtr lParam, ref Boolean handled)
        {
            if (msg == WmDevicechange)
            {
                Debug.WriteLine("Device changed");
                var source = PresentationSource.FromVisual(win) as HwndSource;
                if (source != null)
                    RegisterTouchEvent(source.Handle);
                return new IntPtr(1);
            }

            if (msg == WM_TOUCH)
            {
                handled = HandleTouch(wParam, lParam);
                return new IntPtr(0x0003); // NOACTIVE
            }

            return IntPtr.Zero;
        }

        #endregion Enable touch

        #region Windows API

        public const int DbtDevicearrival = 0x8000;

        // system detected a new device
        public const int DbtDeviceremovecomplete = 0x8004;

        // device is gone
        public const int WmDevicechange = 0x0219;

        // device change event
        private const int DbtDevtypDeviceinterface = 5;

        private const int MAXTOUCHES_INDEX = 0x95;
        private const int SM_DIGITIZER = 94;
        private const uint WM_DISPLAYCHANGE = 0x007e;
        private static readonly Guid GuidDevinterfaceUSBDevice = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        private static readonly Int32 touchInputSize = Marshal.SizeOf(new TOUCHINPUT());

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterTouchWindow(IntPtr hwnd,
        [MarshalAs(UnmanagedType.U4)] RegisterTouchFlags flags);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern void CloseTouchInputHandle(IntPtr lParam);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean GetTouchInputInfo(IntPtr hTouchInput, Int32 cInputs, [In, Out] TOUCHINPUT[] pInputs, Int32 cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr RegisterDeviceNotification(IntPtr recipient, IntPtr notificationFilter, int flags);

        // USB devices
        [DllImport("user32.dll")]
        private static extern bool UnregisterDeviceNotification(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DevBroadcastDeviceinterface
        {
            internal int Size;
            internal int DeviceType;
            internal int Reserved;
            internal Guid ClassGuid;
            internal short Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHINPUT
        {
            public Int32 x;
            public Int32 y;
            public IntPtr hSource;
            public Int32 dwID;
            public Int32 dwFlags;
            public Int32 dwMask;
            public Int32 dwTime;
            public IntPtr dwExtraInfo;
            public Int32 cxContact;
            public Int32 cyContact;
        }

        /*[DllImport("user32",CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern Boolean RegisterTouchWindow(IntPtr hWnd, UInt64 ulFlags);*/

        public enum DWFlags
        {
            TOUCHEVENTF_MOVE = 0x0001,

            TOUCHEVENTF_DOWN = 0x0002,

            TOUCHEVENTF_UP = 0x0004,
        }

        [Flags, Serializable]
        public enum RegisterTouchFlags
        {
            TWF_NONE = 0x00000000,

            TWF_FINETOUCH = 0x00000001,

            TWF_WANTPALM = 0x00000002
        }

        [Flags]
        public enum SM_DIGITIZER_FLAG
        {
            TABLET_CONFIG_NONE = 0x00000000,//	The input digitizer does not have touch capabilities.
            NID_INTEGRATED_TOUCH = 0x00000001,//	An integrated touch digitizer is used for input.
            NID_EXTERNAL_TOUCH = 0x00000002,//	An external touch digitizer is used for input.
            NID_INTEGRATED_PEN = 0x00000004,//	An integrated pen digitizer is used for input.
            NID_EXTERNAL_PEN = 0x00000008,//	An external pen digitizer is used for input.
            NID_MULTI_INPUT = 0x00000040,//	An input digitizer with support for multiple inputs is used for input.
            NID_READY = 0x00000080, //The input digitizer is ready for input. If this value is unset, it may mean that the tablet service is stopped, the digitizer is not supported, or digitizer drivers have not been installed.
        }

        #endregion Windows API

        #region Helpers

        private ConcurrentQueue<TOUCHINPUT[]> TouchQueue = new ConcurrentQueue<TOUCHINPUT[]>();

        private static Int32 LoWord(Int32 number)
        {
            return number & 0xffff;
        }

        //[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private Boolean HandleTouch(IntPtr wParam, IntPtr lParam)
        {
            var inputCount = LoWord(wParam.ToInt32());
            var inputs = new TOUCHINPUT[inputCount];

            if (!GetTouchInputInfo(lParam, inputCount, inputs, touchInputSize))
            {
                Debug.WriteLine("GetTouchInputInfo failed");
                return false;
            }

            //TouchQueue.Enqueue(inputs);
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                ProcessTouchs(inputs, inputCount);
            }));

            CloseTouchInputHandle(lParam);

            return true;
        }

        private void ProcessTouchs(TOUCHINPUT[] inputs, int count)
        {
            var updated = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                var input = inputs[i];

                updated.Add(input.dwID);

                // Assign a handler to this message.
                //Action<TouchArgs> handler = null;
                bool validFlag = false;

                CursorEvent.EventType status = CursorEvent.EventType.UP;

                if ((input.dwFlags & 0x0007) == 0x0005)
                {
                    validFlag = true;
                    status = CursorEvent.EventType.UP;
                }
                else if ((input.dwFlags & 0x0007) == 0x0004)
                {
                    validFlag = true;
                    status = CursorEvent.EventType.UP;
                }
                else if ((input.dwFlags & 0x0007) == 0x0002)
                {
                    validFlag = true;
                    status = CursorEvent.EventType.DOWN;
                }
                else if ((input.dwFlags & 0x0007) == 0x0001)
                {
                    validFlag = true;
                    status = CursorEvent.EventType.MOVE;
                }

                long time = Stopwatch.GetTimestamp(); ;

                // Convert message parameters into touch event arguments and handle the event.
                if (validFlag)
                {
                    //var pt = win.PointFromScreen(new Point(input.x * 0.01, input.y * 0.01));
                    var pt = new Point(input.x * 0.01, input.y * 0.01);

                    var e = new TouchArgs
                    {
                        // TOUCHINFO point coordinates and contact size is in 1/100 of a pixel; convert it to pixels.
                        // Also convert screen to client coordinates.
                        ContactY = input.cyContact * 0.01,
                        ContactX = input.cxContact * 0.01,
                        Id = input.dwID,
                        LocationX = pt.X,
                        LocationY = pt.Y,
                        //Time = input.dwTime,
                        Time = time, //DateTime.Now.Ticks / 10000,
                        Mask = input.dwMask,
                        Flags = input.dwFlags,
                        Count = count,
                        Status = status
                    };

                    ProcessTouchArg(e, time);
                }
                else
                {
                    Debug.WriteLine("Unknow flag for touch data");
                }
            }

            //check missed event
            /*var toRemove = new List<int>(_idMapping.Count);
            foreach (int id in _idMapping.Keys)
            {
                if (!updated.Contains(id))
                    toRemove.Add(id);
            }

            foreach (int id in toRemove)
            {
                var c = new Cursor(_idMapping[id], 0, 0, Stopwatch.GetTimestamp(), Cursor.CursorState.Up);
                Debug.WriteLine("missed event. hard id : " + id + " soft id : " + c.Id);
                var arg = new TouchInputEventArgs(c);
                this.OnRemoveInput(arg);
                _idMapping.Remove(id);
            }*/
        }

        private struct TouchMessage
        {
            public int Count
            { get; private set; }

            public TOUCHINPUT[] inputs
            { get; private set; }

            public TouchMessage(TOUCHINPUT[] inputs, int count)
                : this()
            {
                this.inputs = inputs;
                this.Count = count;
            }
        }

        #endregion Helpers
    }
}
