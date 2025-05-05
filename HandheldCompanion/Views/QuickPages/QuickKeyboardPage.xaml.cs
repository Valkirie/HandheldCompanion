using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace HandheldCompanion.Views.QuickPages
{
    public partial class QuickKeyboardPage : Page
    {
        // Constructors
        public QuickKeyboardPage(string tag) : this() => Tag = tag;
        public QuickKeyboardPage()
        {
            InitializeComponent();
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
        private static readonly uint[] _row1Sc = { 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19 };
        private static readonly uint[] _row2Sc = { 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27 };
        private static readonly uint[] _row3Sc = { 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32 }; // Z,X,C,V,B,N,M positions

        // Numeric keys scan-codes
        private static readonly uint[] _numSc = { 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B };

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
            var h = GetKeyboardLayout(tid);
            if (h != _lastHkl) { _lastHkl = h; Build(); }
        }

        // Build all rows based on current _state
        private void Build()
        {
            Row1Panel.Children.Clear();
            Row2Panel.Children.Clear();
            Row3Panel.Children.Clear();
            Row4Panel.Children.Clear();
            _shiftToggled = false;

            switch (_state)
            {
                case LayoutState.Default:
                    BuildDynamicRow(Row1Panel, _row1Sc);
                    BuildDynamicRow(Row2Panel, _row2Sc);
                    AddShiftToggle(Row3Panel);
                    BuildDynamicRow(Row3Panel, _row3Sc);
                    AddBackspace(Row3Panel);
                    AddSwitchKey(Row4Panel, "?123", LayoutState.Switch1);
                    AddScanKey(Row4Panel, 0xBC); // comma
                    AddUnicodeKey(Row4Panel, ' ');
                    AddScanKey(Row4Panel, 0xBE); // period
                    AddReturn(Row4Panel);
                    break;

                case LayoutState.Switch1:
                    // Row1: digits 1-0 as Unicode
                    BuildUnicodeRow(Row1Panel, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" });
                    // Row2: symbols ! @ # $ € & _ - = +
                    BuildUnicodeRow(Row2Panel, new[] { "!", "@", "#", "$", "€", "&", "_", "-", "=", "+" });
                    // Row3: SWITCH2 button, then ; : ( ) / ' " BACKSPACE
                    AddSwitchKey(Row3Panel, "SWITCH2", LayoutState.Switch2);
                    BuildUnicodeRow(Row3Panel, new[] { ";", ":", "(", ")", "/", "'", "\"" });
                    AddBackspace(Row3Panel);
                    // Row4: SWITCH (default), comma, space, period, question mark, return
                    AddSwitchKey(Row4Panel, "SWITCH", LayoutState.Default);
                    BuildUnicodeRow(Row4Panel, new[] { ",", "+" });
                    // Correction:
                    Row4Panel.Children.Clear(); // Clear any accidental additions
                    AddSwitchKey(Row4Panel, "SWITCH", LayoutState.Default);
                    AddUnicodeKey(Row4Panel, ',');
                    AddUnicodeKey(Row4Panel, ' ');
                    AddUnicodeKey(Row4Panel, '.');
                    AddUnicodeKey(Row4Panel, '?');
                    AddReturn(Row4Panel);
                    break;

                case LayoutState.Switch2:
                    // Row1: 1-0
                    BuildUnicodeRow(Row1Panel, new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" });
                    // Row2: % [ ] { } < > ^ £ ¥
                    BuildUnicodeRow(Row2Panel, new[] { "%", "[", "]", "{", "}", "<", ">", "^", "£", "¥" });
                    // Row3: SWITCH1 button, then * § « » ~ |
                    AddSwitchKey(Row3Panel, "SWITCH1", LayoutState.Switch1);
                    BuildUnicodeRow(Row3Panel, new[] { "*", "§", "«", "»", "~", "|" });
                    AddBackspace(Row3Panel);
                    // Row4: SWITCH (default), comma, space, backslash, return
                    Row4Panel.Children.Clear();
                    AddSwitchKey(Row4Panel, "SWITCH", LayoutState.Default);
                    AddUnicodeKey(Row4Panel, ',');
                    AddUnicodeKey(Row4Panel, ' ');
                    AddUnicodeKey(Row4Panel, '\\');
                    AddReturn(Row4Panel);
                    break;
            }

            RelabelAll();
        }

        // Dynamic row: physical scan-codes, MapVirtualKeyEx + ToUnicodeEx for label
        private void BuildDynamicRow(Panel p, uint[] scans)
        {
            foreach (var sc in scans)
            {
                var b = new Button { Tag = (sc, false), Width = 36, Height = 60, Margin = new Thickness(2) };
                b.Click += ScanKey_Click;
                p.Children.Add(b);
            }
        }

        // Static scan row (digits with forced SHIFT)
        private void BuildScanRow(Panel p, uint[] scans, bool forceShift)
        {
            foreach (var sc in scans)
            {
                var b = new Button { Tag = (sc, forceShift), Width = 36, Height = 60, Margin = new Thickness(2) };
                b.Click += ScanKey_Click;
                p.Children.Add(b);
            }
        }

        // Static unicode row
        private void BuildUnicodeRow(Panel p, string[] labels)
        {
            foreach (string label in labels)
            {
                var b = new Button { Tag = label.ToCharArray()[0], Width = 36, Height = 60, Margin = new Thickness(2), Content = label };
                b.Click += Unicode_Click;
                p.Children.Add(b);
            }
        }
        private void BuildUnicodeRow(Panel p, IEnumerable<string> labels) => BuildUnicodeRow(p, labels is string[] arr ? arr : new List<string>(labels).ToArray());
        private void AddUnicodeRowSymbols(Panel p, string[] syms) => BuildUnicodeRow(p, syms);

        // Helpers to add function keys
        private void AddShiftToggle(Panel p)
        {
            var tb = new ToggleButton { Width = 60, Height = 60, Margin = new Thickness(2), Content = "⇧" };
            tb.Checked += (s, e) => { _shiftToggled = true; RelabelAll(); };
            tb.Unchecked += (s, e) => { _shiftToggled = false; RelabelAll(); };
            p.Children.Add(tb);
        }
        private void AddBackspace(Panel p) => AddScanKey(p, 0x0E, "⌫");
        private void AddReturn(Panel p) => AddScanKey(p, 0x1C, "⏎");
        private void AddUnicodeKey(Panel p, char c)
        {
            var b = new Button { Tag = (object)c, Width = 36, Height = 60, Margin = new Thickness(2), Content = c.ToString() };
            b.Click += Unicode_Click;
            p.Children.Add(b);
        }
        private void AddScanKey(Panel p, uint sc, string label = null)
        {
            var b = new Button { Tag = (sc, false), Width = 36, Height = 60, Margin = new Thickness(2) };
            if (label != null) b.Content = label;
            b.Click += ScanKey_Click;
            p.Children.Add(b);
        }
        private void AddSwitchKey(Panel p, string text, LayoutState tgt)
        {
            var b = new Button { Content = text, Width = 60, Height = 60, Margin = new Thickness(2) };
            b.Click += (s, e) => { _state = tgt; Build(); };
            p.Children.Add(b);
        }

        // Relabel dynamic buttons
        private void RelabelAll()
        {
            byte[] ks = new byte[256];
            GetKeyboardState(ks);
            if (_shiftToggled) ks[0x10] = 0x80; // SHIFT keycode

            foreach (var panel in new[] { Row1Panel, Row2Panel, Row3Panel })
                foreach (var child in panel.Children)
                {
                    if (child is Button b && b.Tag is ValueTuple<uint, bool> t)
                    {
                        var (sc, fs) = t;
                        byte[] st = (byte[])ks.Clone();
                        if (fs) st[0x10] = 0x80;
                        uint vk = MapVirtualKeyEx(sc, MAPVK_VSC_TO_VK_EX, _lastHkl);
                        var sb = new StringBuilder(2);
                        int cnt = ToUnicodeEx(vk, sc, st, sb, sb.Capacity, 0, _lastHkl);
                        b.Content = cnt > 0 ? sb[0].ToString() : ((Key)KeyInterop.KeyFromVirtualKey((int)vk)).ToString();
                    }
                }
        }

        // Scan key click => restores focus and sends scan codes
        private void ScanKey_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button b && b.Tag is ValueTuple<uint, bool> t)) return;
            var (sc, fs) = t;

            fs = _shiftToggled;

            var seq = new List<INPUT>();
            if (fs) seq.Add(MakeScan(0x2A, false)); // SHIFT down
            seq.Add(MakeScan(sc, false));            // key down
            seq.Add(MakeScan(sc, true));             // key up
            if (fs) seq.Add(MakeScan(0x2A, true));   // SHIFT up
            SendInput((uint)seq.Count, seq.ToArray(), Marshal.SizeOf<INPUT>());
        }

        // Unicode key click (static symbols)
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

        private void SendScan(uint scanCode)
        {
            const uint INPUT_KEYBOARD = 1;
            const uint KEYEVENTF_SCANCODE = 0x0008;
            const uint KEYEVENTF_KEYUP = 0x0002;

            var inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = (ushort)scanCode,
                dwFlags = KEYEVENTF_SCANCODE,
                time = 0,
                dwExtraInfo = GetMessageExtraInfo()
            };

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = (ushort)scanCode,
                dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = GetMessageExtraInfo()
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
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
    }
}