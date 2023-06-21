using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using static PInvoke.User32;

namespace HandheldCompanion.Views.Classes
{
    public class GamepadWindow : Window
    {
        public List<Control> elements = new();
        protected GamepadFocusManager gamepadFocusManager;

        public GamepadWindow()
        {
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            return IntPtr.Zero;

            // REVERSE ENGINEERING KEYBOARD FOCUS RECTANGLE LOGIC
            if (!Title.Equals("QuickTools"))
                return IntPtr.Zero;

            switch (msg)
            {
                case 6:
                case 7:
                case 8:
                case 13:
                case 28:
                case 32:
                case 33:
                case 61:
                case 70:
                case 71:
                case 132:
                case 134:
                case 160:
                case 512:
                case 513:
                case 514:
                case 641:
                case 642:
                case 674:
                    return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            // Track when objects are added and removed
            if (visualAdded != null && visualAdded.GetType() == typeof(Control))
                elements.Add((Control)visualAdded);

            if (visualRemoved != null && visualRemoved.GetType() == typeof(Control))
                elements.Remove((Control)visualRemoved);

            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            elements = WPFUtils.FindChildren(this);
        }
    }
}
