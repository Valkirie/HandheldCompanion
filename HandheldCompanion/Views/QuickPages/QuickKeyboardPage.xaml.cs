using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HandheldCompanion.Views.QuickPages
{
    public partial class QuickKeyboardPage : Page
    {
        // Physical scan‐codes for a standard 101/102-key PC keyboard,
        // grouped by row (letter-only, left-to-right).
        private readonly uint[] _row1Sc = { 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19 };
        private readonly uint[] _row2Sc = { 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27 };
        private readonly uint[] _row3Sc = { 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35 };

        private readonly List<Button> _letterButtons = new();
        private readonly DispatcherTimer _layoutPollTimer;
        private IntPtr _lastHkl = IntPtr.Zero;

        // Use extended mapping so we get the true layout-specific VK
        private const uint MAPVK_VSC_TO_VK_EX = 3;

        public QuickKeyboardPage(string tag) : this() => this.Tag = tag;
        public QuickKeyboardPage()
        {
            InitializeComponent();

            _layoutPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _layoutPollTimer.Tick += LayoutPollTimer_Tick;
            _layoutPollTimer.Start();

            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            uint tid = GetWindowThreadProcessId(fg, out _);
            _lastHkl = GetKeyboardLayout(tid);
            BuildKeyboard();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _layoutPollTimer.Stop();
        }

        private void LayoutPollTimer_Tick(object? sender, EventArgs e)
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return;

            uint tid = GetWindowThreadProcessId(fg, out _);
            var hkl = GetKeyboardLayout(tid);
            if (hkl != _lastHkl)
            {
                _lastHkl = hkl;
                BuildKeyboard();
            }
        }

        private void BuildKeyboard()
        {
            Row1Panel.Children.Clear();
            Row2Panel.Children.Clear();
            Row3Panel.Children.Clear();
            _letterButtons.Clear();

            Button MakeButton(uint scanCode)
            {
                var btn = new Button
                {
                    Tag = scanCode,
                    Width = 36,
                    Height = 60,
                    Margin = new Thickness(2)
                };
                btn.Click += KeyButton_Click;
                return btn;
            }

            foreach (var sc in _row1Sc)
            {
                var b = MakeButton(sc);
                Row1Panel.Children.Add(b);
                _letterButtons.Add(b);
            }
            foreach (var sc in _row2Sc)
            {
                var b = MakeButton(sc);
                Row2Panel.Children.Add(b);
                _letterButtons.Add(b);
            }
            foreach (var sc in _row3Sc)
            {
                var b = MakeButton(sc);
                Row3Panel.Children.Add(b);
                _letterButtons.Add(b);
            }

            RelabelLetters();
        }

        private void RelabelLetters()
        {
            var hkl = _lastHkl;
            var keyState = new byte[256];
            GetKeyboardState(keyState);

            UIHelper.TryInvoke(() =>
            {
                foreach (var btn in _letterButtons)
                {
                    uint scan = (uint)btn.Tag;
                    uint vk = MapVirtualKeyEx(scan, MAPVK_VSC_TO_VK_EX, hkl);

                    var uniBuf = new StringBuilder(5);
                    int count = ToUnicodeEx(vk, scan, keyState, uniBuf, uniBuf.Capacity, 0, hkl);

                    string label;
                    if (count > 0)
                    {
                        label = uniBuf[0].ToString();
                    }
                    else
                    {
                        // non-printing keys (F1, arrows, etc.)
                        int lParam = (int)(scan << 16);
                        var nameBuf = new StringBuilder(128);
                        GetKeyNameText(lParam, nameBuf, nameBuf.Capacity);
                        label = nameBuf.ToString();
                    }

                    btn.Content = label;
                }
            });
        }

        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
            uint scan = (uint)((Button)sender).Tag;
            SendScan(scan);
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

        #region P/Invokes

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(
            uint wVirtKey, uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex = 4)]
            StringBuilder pwszBuff,
            int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern UIntPtr GetMessageExtraInfo();

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

        #endregion
    }
}