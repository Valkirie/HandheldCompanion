using HandheldCompanion.Helpers;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages
{
    public partial class QuickKeyboardPage : Page
    {
        // the one animation template
        private readonly Storyboard _tapTemplate;

        // Constructors
        public QuickKeyboardPage(string tag) : this() => Tag = tag;
        public QuickKeyboardPage()
        {
            DataContext = new QuickKeyboardPageViewModel(this);
            InitializeComponent();

            // grab the *templates* from your XAML resources
            _tapTemplate = (Storyboard)TryFindResource("KeyTapAnimation");

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // Layout modes
        private enum LayoutState { Default, Switch1, Switch2 }
        private LayoutState _state = LayoutState.Default;

        private bool _shiftToggled
        {
            get => ((QuickKeyboardPageViewModel)DataContext).ShiftToggleChecked;
            set => ((QuickKeyboardPageViewModel)DataContext).ShiftToggleChecked = value;
        }

        private bool _shiftToggleLocked
        {
            get => ((QuickKeyboardPageViewModel)DataContext).ShiftToggleLocked;
        }

        // Original target window to restore focus
        private readonly DispatcherTimer _timer;
        private IntPtr _lastHkl;

        // Physical scan-codes for default letter layout (dynamic per HKL)
        private static readonly object[] _row0Sc = { (0x01, KEYEVENTF_SCANCODE), (0x0F, KEYEVENTF_SCANCODE), (0x3B, KEYEVENTF_SCANCODE), (0x3C, KEYEVENTF_SCANCODE), (0x3D, KEYEVENTF_SCANCODE), (0x3E, KEYEVENTF_SCANCODE), (0x4B, KEYEVENTF_SCANCODE_EXT), (0x4D, KEYEVENTF_SCANCODE_EXT), (0x53, KEYEVENTF_SCANCODE_EXT) };                           // ESC, TAB, F1, F2, F3, F4, LEFT, RIGHT, DEL
        private static readonly object[] _row1Sc = { (0x10, KEYEVENTF_SCANCODE), (0x11, KEYEVENTF_SCANCODE), (0x12, KEYEVENTF_SCANCODE), (0x13, KEYEVENTF_SCANCODE), (0x14, KEYEVENTF_SCANCODE), (0x15, KEYEVENTF_SCANCODE), (0x16, KEYEVENTF_SCANCODE), (0x17, KEYEVENTF_SCANCODE), (0x18, KEYEVENTF_SCANCODE), (0x19, KEYEVENTF_SCANCODE) };   // Q,W,E,R,T,Y,U,I,O,P
        private static readonly object[] _row2Sc = { (0x1E, KEYEVENTF_SCANCODE), (0x1F, KEYEVENTF_SCANCODE), (0x20, KEYEVENTF_SCANCODE), (0x21, KEYEVENTF_SCANCODE), (0x22, KEYEVENTF_SCANCODE), (0x23, KEYEVENTF_SCANCODE), (0x24, KEYEVENTF_SCANCODE), (0x25, KEYEVENTF_SCANCODE), (0x26, KEYEVENTF_SCANCODE), (0x27, KEYEVENTF_SCANCODE) };   // A,S,D,F,G,H,J,K,L,;
        private static readonly object[] _row3Sc = { (0x00, KEYEVENTF_SCANCODE), (0x2C, KEYEVENTF_SCANCODE), (0x2D, KEYEVENTF_SCANCODE), (0x2E, KEYEVENTF_SCANCODE), (0x2F, KEYEVENTF_SCANCODE), (0x30, KEYEVENTF_SCANCODE), (0x31, KEYEVENTF_SCANCODE), (0x32, KEYEVENTF_SCANCODE), (0x0E, KEYEVENTF_SCANCODE) };                               // SHIFT,Z,X,C,V,B,N,M,BACKSPACE
        private static readonly object[] _row4Sc = { (string.Empty, KEYEVENTF_UNICODE), (",", KEYEVENTF_UNICODE), (" ", KEYEVENTF_UNICODE), (".", KEYEVENTF_UNICODE), ("?", KEYEVENTF_UNICODE), (0x1C, KEYEVENTF_SCANCODE) };                                                                                                                           // SWITCH,COMMA,SPACE,LANGUAGE,PERIOD,RETURN

        // P/Invoke
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MAPVK_VSC_TO_VK_EX = 3;
        private const uint KEYEVENTF_SCANCODE_EXT = KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY;

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint threadId);
        [DllImport("user32.dll")] static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);
        [DllImport("user32.dll")] static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ToUnicodeEx(uint vKey, uint scanCode, byte[] state, [Out] StringBuilder buf, int bufSize, uint flags, IntPtr layout);
        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] static extern UIntPtr GetMessageExtraInfo();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public UIntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT { public uint uMsg; public ushort wParamL, wParamH; }

        // Page events
        private void Page_Loaded(object s, RoutedEventArgs e)
        {
            Timer_Tick(s, e); // initial build
        }

        private void Page_Unloaded(object s, RoutedEventArgs e) => _timer.Stop();

        // Poll Windows HKL changes
        private void Timer_Tick(object sender, EventArgs e)
        {
            nint _targetHwnd = GetForegroundWindow();
            if (_targetHwnd == IntPtr.Zero) return;

            uint tid = GetWindowThreadProcessId(_targetHwnd, out _);
            nint h = GetKeyboardLayout(tid);
            if (h != _lastHkl)
            {
                _lastHkl = h;
                UIHelper.TryInvoke(() => { Build(); });
            }
        }

        private void LayoutSwitch_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case LayoutState.Switch1:
                    _state = LayoutState.Switch2;
                    break;
                case LayoutState.Switch2:
                    _state = LayoutState.Switch1;
                    break;
            }

            Build();
        }

        private void SwitchTo123_Click(object sender, RoutedEventArgs e)
        {
            switch (_state)
            {
                case LayoutState.Default:
                    SwitchTo123.Content = "abc";
                    _state = LayoutState.Switch1;
                    break;
                case LayoutState.Switch1:
                case LayoutState.Switch2:
                    SwitchTo123.Content = "&123";
                    _state = LayoutState.Default;
                    break;
            }

            Build();
        }

        private void Build()
        {
            // reset vars
            _shiftToggled = false;

            switch (_state)
            {
                case LayoutState.Default:
                    {
                        ShiftToggle.Visibility = Visibility.Visible;
                        LayoutSwitch.Visibility = Visibility.Collapsed;
                        BuildDynamicRow(Row0Panel, _row0Sc);
                        BuildDynamicRow(Row1Panel, _row1Sc);
                        BuildDynamicRow(Row2Panel, _row2Sc);
                        BuildDynamicRow(Row3Panel, _row3Sc);
                        BuildDynamicRow(Row4Panel, _row4Sc);
                    }
                    break;
                case LayoutState.Switch1:
                    {
                        ShiftToggle.Visibility = Visibility.Collapsed;
                        LayoutSwitch.Visibility = Visibility.Visible;
                        BuildDynamicRow(Row1Panel, [("1", KEYEVENTF_UNICODE), ("2", KEYEVENTF_UNICODE), ("3", KEYEVENTF_UNICODE), ("4", KEYEVENTF_UNICODE), ("5", KEYEVENTF_UNICODE), ("6", KEYEVENTF_UNICODE), ("7", KEYEVENTF_UNICODE), ("8", KEYEVENTF_UNICODE), ("9", KEYEVENTF_UNICODE), ("0", KEYEVENTF_UNICODE)]);
                        BuildDynamicRow(Row2Panel, [("!", KEYEVENTF_UNICODE), ("@", KEYEVENTF_UNICODE), ("#", KEYEVENTF_UNICODE), ("$", KEYEVENTF_UNICODE), ("€", KEYEVENTF_UNICODE), ("&", KEYEVENTF_UNICODE), ("_", KEYEVENTF_UNICODE), ("-", KEYEVENTF_UNICODE), ("=", KEYEVENTF_UNICODE), ("+", KEYEVENTF_UNICODE)]);
                        BuildDynamicRow(Row3Panel, [(string.Empty, KEYEVENTF_UNICODE), (";", KEYEVENTF_UNICODE), (":", KEYEVENTF_UNICODE), ("(", KEYEVENTF_UNICODE), (")", KEYEVENTF_UNICODE), ("/", KEYEVENTF_UNICODE), ("'", KEYEVENTF_UNICODE), ("\"", KEYEVENTF_UNICODE), (string.Empty, KEYEVENTF_UNICODE)]);
                        BuildDynamicRow(Row4Panel, [(string.Empty, KEYEVENTF_UNICODE), (",", KEYEVENTF_UNICODE), (" ", KEYEVENTF_UNICODE), (".", KEYEVENTF_UNICODE), ("?", KEYEVENTF_UNICODE), (0x1C, KEYEVENTF_SCANCODE)]);
                        if (LayoutSwitch.Content is FontIcon fontIcon)
                            fontIcon.Glyph = "\ue761";
                    }
                    break;
                case LayoutState.Switch2:
                    {
                        ShiftToggle.Visibility = Visibility.Collapsed;
                        LayoutSwitch.Visibility = Visibility.Visible;
                        BuildDynamicRow(Row1Panel, [("1", KEYEVENTF_UNICODE), ("2", KEYEVENTF_UNICODE), ("3", KEYEVENTF_UNICODE), ("4", KEYEVENTF_UNICODE), ("5", KEYEVENTF_UNICODE), ("6", KEYEVENTF_UNICODE), ("7", KEYEVENTF_UNICODE), ("8", KEYEVENTF_UNICODE), ("9", KEYEVENTF_UNICODE), ("0", KEYEVENTF_UNICODE)]);
                        BuildDynamicRow(Row2Panel, [("%", KEYEVENTF_UNICODE), ("[", KEYEVENTF_UNICODE), ("]", KEYEVENTF_UNICODE), ("{", KEYEVENTF_UNICODE), ("}", KEYEVENTF_UNICODE), ("<", KEYEVENTF_UNICODE), (">", KEYEVENTF_UNICODE), ("^", KEYEVENTF_UNICODE), ("£", KEYEVENTF_UNICODE), ("¥", KEYEVENTF_UNICODE)]);
                        BuildDynamicRow(Row3Panel, [(string.Empty, KEYEVENTF_UNICODE), ("*", KEYEVENTF_UNICODE), ("`", KEYEVENTF_UNICODE), ("§", KEYEVENTF_UNICODE), ("«", KEYEVENTF_UNICODE), ("»", KEYEVENTF_UNICODE), ("~", KEYEVENTF_UNICODE), ("|", KEYEVENTF_UNICODE), (string.Empty, KEYEVENTF_UNICODE)]);
                        if (LayoutSwitch.Content is FontIcon fontIcon)
                            fontIcon.Glyph = "\ue760";
                    }
                    break;
            }

            RelabelAll();
        }

        private void BuildDynamicRow(Panel p, object[] objects)
        {
            for (int i = 0; i < p.Children.Count; i++)
            {
                object o = objects[i];

                if (p.Children[i] is Button button)
                {
                    if (o is ValueTuple<int, uint> it)
                    {
                        (int sc, uint ext) = it;
                        if (sc == 0x00)
                            continue; // skip empty buttons

                        button.Tag = (sc, ext);
                    }
                    else if (o is ValueTuple<string, uint> st)
                    {
                        (string s, uint ext) = st;
                        if (string.IsNullOrEmpty(s))
                            continue;

                        char c = s.ToCharArray()[0];

                        button.Tag = (c, ext);
                        if (!string.IsNullOrEmpty(s))
                        {
                            if (button.Content is FontIcon fontIcon)
                                fontIcon.Glyph = s;
                            else
                                button.Content = s;
                        }
                    }

                    // reset event
                    button.Click -= Button_Click;
                    button.Click += Button_Click;
                }
            }
        }

        // Relabel dynamic buttons
        public void RelabelAll()
        {
            byte[] ks = new byte[256];
            GetKeyboardState(ks);
            ks[0x10] = (byte)(_shiftToggled ? 0x80 : 0x00); // SHIFT keycode

            foreach (Grid? panel in new[] { Row1Panel, Row2Panel, Row3Panel, Row4Panel })
            {
                foreach (object? child in panel.Children)
                {
                    if (child is Button b && b.Tag is ValueTuple<int, uint> it)
                    {
                        if (!string.IsNullOrEmpty(b.Name))
                            continue; // skip named buttons

                        (int sc, uint ext) = it;

                        byte[] st = (byte[])ks.Clone();
                        st[0x10] = (byte)(_shiftToggled ? 0x80 : 0x00); // SHIFT keycode
                        uint vk = MapVirtualKeyEx((uint)sc, MAPVK_VSC_TO_VK_EX, _lastHkl);
                        StringBuilder sb = new StringBuilder(2);
                        int cnt = ToUnicodeEx(vk, (uint)sc, st, sb, sb.Capacity, 0, _lastHkl);

                        string content = sb[0].ToString();
                        if (cnt > 0 && !string.IsNullOrEmpty(content))
                            b.Content = content;
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            List<INPUT> seq = new List<INPUT>();

            if (sender is Button b)
            {
                if (b.Tag is ValueTuple<int, uint> it)
                {
                    if (_shiftToggled) seq.Add(MakeScan(0x2A, KEYEVENTF_SCANCODE, false));      // Shift down
                    seq.Add(MakeScan((uint)it.Item1, it.Item2, false));                         // Key down
                    seq.Add(MakeScan((uint)it.Item1, it.Item2, true));                          // Key up
                    if (_shiftToggled) seq.Add(MakeScan(0x2A, KEYEVENTF_SCANCODE, true));       // Shift up
                }
                else if (b.Tag is ValueTuple<char, uint> ct)
                {
                    seq.Add(MakeScan(ct.Item1, ct.Item2, false));                         // Key down
                    seq.Add(MakeScan(ct.Item1, ct.Item2, true));                          // Key up
                }
            }

            SendInput((uint)seq.Count, seq.ToArray(), Marshal.SizeOf<INPUT>());

            // disable shift toggle (if not locked)
            if (!_shiftToggleLocked)
                _shiftToggled = false;
        }

        private static INPUT MakeScan(uint scanCode, uint ext, bool up)
        {
            INPUT input = new();

            input.type = INPUT_KEYBOARD;
            input.U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = (ushort)scanCode,
                dwFlags = ext | (up ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = GetMessageExtraInfo()
            };

            return input;
        }

        private void Key_Tap(object sender, RoutedEventArgs e)
        {
            if (_tapTemplate == null) return;
            if (!(sender is FrameworkElement el)) return;

            // clone so back-to-back presses don't stomp on each other
            var sb = _tapTemplate.Clone();
            foreach (Timeline tl in sb.Children)
                Storyboard.SetTarget(tl, el);

            // start the full shrink->grow on this key
            sb.Begin(el, true);
        }
    }
}