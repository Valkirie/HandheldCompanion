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
        private bool _shiftToggled;

        // Original target window to restore focus
        private IntPtr _targetHwnd;
        private readonly DispatcherTimer _timer;
        private IntPtr _lastHkl;

        // Physical scan-codes for default letter layout (dynamic per HKL)
        private static readonly object[] _row1Sc = { 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19 };      // Q,W,E,R,T,Y,U,I,O,P
        private static readonly object[] _row2Sc = { 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27 };      // A,S,D,F,G,H,J,K,L,;
        private static readonly object[] _row3Sc = { 0x00, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x0E };             // SHIFT,Z,X,C,V,B,N,M,BACKSPACE
        private static readonly object[] _row4Sc = { string.Empty, ",", " ", ".", "?", 0x1C };                          // SWITCH,COMMA,SPACE,LANGUAGE,PERIOD,RETURN

        // P/Invoke
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MAPVK_VSC_TO_VK_EX = 3;

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
            // store target and layout
            _targetHwnd = GetForegroundWindow();
            if (_targetHwnd == IntPtr.Zero) return;
            uint tid = GetWindowThreadProcessId(_targetHwnd, out _);
            _lastHkl = GetKeyboardLayout(tid);
            Build();
        }

        private void Page_Unloaded(object s, RoutedEventArgs e) => _timer.Stop();

        // Poll Windows HKL changes
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_targetHwnd == IntPtr.Zero) return;

            uint tid = GetWindowThreadProcessId(_targetHwnd, out _);
            nint h = GetKeyboardLayout(tid);
            if (h != _lastHkl)
            {
                _lastHkl = h;
                Build();
            }
        }

        private void ShiftToggle_Checked(object sender, RoutedEventArgs e)
        {
            _shiftToggled = true; 
            RelabelAll();
        }

        private void ShiftToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _shiftToggled = false;
            RelabelAll();
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
            switch(_state)
            {
                case LayoutState.Default:
                    _state = LayoutState.Switch1;
                    break;
                case LayoutState.Switch1:
                case LayoutState.Switch2:
                    _state = LayoutState.Default;
                    break;
            }

            Build();
        }

        private void Build()
        {
            // reset vars
            _shiftToggled = false;
            // todo: use MVVM
            ShiftToggle.IsChecked = _shiftToggled;

            switch (_state)
            {
                case LayoutState.Default:
                    {
                        ShiftToggle.Visibility = Visibility.Visible;
                        LayoutSwitch.Visibility = Visibility.Collapsed;
                        BuildDynamicRow(Row1Panel, _row1Sc);
                        BuildDynamicRow(Row2Panel, _row2Sc);
                        BuildDynamicRow(Row3Panel, _row3Sc);
                    }
                    break;
                case LayoutState.Switch1:
                    {
                        ShiftToggle.Visibility = Visibility.Collapsed;
                        LayoutSwitch.Visibility = Visibility.Visible;
                        BuildDynamicRow(Row1Panel, ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"]);
                        BuildDynamicRow(Row2Panel, ["!", "@", "#", "$", "€", "&", "_", "-", "=", "+"]);
                        BuildDynamicRow(Row3Panel, [string.Empty, ";", ":", "(", ")", "/", "'", "\"", string.Empty]);
                        if (LayoutSwitch.Content is FontIcon fontIcon)
                            fontIcon.Glyph = "\ue761";
                    }
                    break;
                case LayoutState.Switch2:
                    {
                        ShiftToggle.Visibility = Visibility.Collapsed;
                        LayoutSwitch.Visibility = Visibility.Visible;
                        BuildDynamicRow(Row1Panel, ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"]);
                        BuildDynamicRow(Row2Panel, ["%", "[", "]", "{", "}", "<", ">", "^", "£", "¥"]);
                        BuildDynamicRow(Row3Panel, [string.Empty, "*", "`", "§", "«", "»", "~", "|", string.Empty]);
                        if (LayoutSwitch.Content is FontIcon fontIcon)
                            fontIcon.Glyph = "\ue760";
                    }
                    break;
            }

            BuildDynamicRow(Row4Panel, _row4Sc);

            RelabelAll();
        }

        private void BuildDynamicRow(Panel p, object[] objects)
        {
            for (int i = 0; i < p.Children.Count; i++)
            {
                object o = objects[i];
                if (o is null)
                    continue;

                if (o is int scan)
                {
                    if (scan == 0x00)
                        continue; // skip empty buttons

                    if (p.Children[i] is Button button)
                    {
                        button.Tag = ((uint)scan, false);

                        // dirty
                        button.Click -= ScanKey_Click;
                        button.Click -= Unicode_Click;

                        button.Click += ScanKey_Click;
                    }
                }
                else if (o is string s)
                {
                    if (string.IsNullOrEmpty(s))
                        continue;

                    char c = s.ToCharArray()[0];
                    if (p.Children[i] is Button button)
                    {
                        button.Tag = c;
                        if (!string.IsNullOrEmpty(s))
                        {
                            if (button.Content is FontIcon fontIcon)
                                fontIcon.Glyph = s;
                            else
                                button.Content = s;
                        }

                        // dirty
                        button.Click -= ScanKey_Click;
                        button.Click -= Unicode_Click;

                        button.Click += Unicode_Click;
                    }
                }
            }
        }

        // Relabel dynamic buttons
        private void RelabelAll()
        {
            byte[] ks = new byte[256];
            GetKeyboardState(ks);
            if (_shiftToggled) ks[0x10] = 0x80; // SHIFT keycode

            foreach (Grid? panel in new[] { Row1Panel, Row2Panel, Row3Panel, Row4Panel })
            {
                foreach (object? child in panel.Children)
                {
                    if (child is Button b && b.Tag is ValueTuple<uint, bool> t)
                    {
                        if (!string.IsNullOrEmpty(b.Name))
                            continue; // skip named buttons

                        (uint sc, bool fs) = t;
                        byte[] st = (byte[])ks.Clone();
                        if (fs) st[0x10] = 0x80;
                        uint vk = MapVirtualKeyEx(sc, MAPVK_VSC_TO_VK_EX, _lastHkl);
                        StringBuilder sb = new StringBuilder(2);
                        int cnt = ToUnicodeEx(vk, sc, st, sb, sb.Capacity, 0, _lastHkl);

                        string content = sb[0].ToString();
                        if (cnt > 0 && !string.IsNullOrEmpty(content))
                            b.Content = content;
                    }
                }
            }
        }

        private void ScanKey_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b && b.Tag is ValueTuple<uint, bool> t)) return;
            var (sc, fs) = t;

            fs = _shiftToggled;

            var seq = new List<INPUT>();
            if (fs) seq.Add(MakeScan(0x2A, false));     // Shift down
            seq.Add(MakeScan(sc, false));               // Key down
            seq.Add(MakeScan(sc, true));                // Key up
            if (fs) seq.Add(MakeScan(0x2A, true));      // Shift up
            SendInput((uint)seq.Count, seq.ToArray(), Marshal.SizeOf<INPUT>());
        }

        private void Unicode_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b && b.Tag is char c)) return;

            // send Unicode via KEYEVENTF_UNICODE
            const uint KEYEVENTF_UNICODE = 0x0004;
            var down = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE, dwExtraInfo = GetMessageExtraInfo() } } };
            var up = new INPUT { type = INPUT_KEYBOARD, U = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP, dwExtraInfo = GetMessageExtraInfo() } } };
            SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
            SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
        }

        private static INPUT MakeScan(uint scanCode, bool up)
        {
            INPUT input = new();

            input.type = INPUT_KEYBOARD;
            input.U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = (ushort)scanCode,
                dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = GetMessageExtraInfo()
            };

            return input;
        }

        private void Key_Tap(object sender, RoutedEventArgs e)
        {
            if (_tapTemplate == null) return;
            if (!(sender is FrameworkElement el)) return;

            // clone so back-to-back presses don’t stomp on each other
            var sb = _tapTemplate.Clone();
            foreach (Timeline tl in sb.Children)
                Storyboard.SetTarget(tl, el);

            // start the full shrink→grow on this key
            sb.Begin(el, true);
        }
    }
}