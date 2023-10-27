using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            // Track when objects are added and removed
            if (visualAdded != null && visualAdded is Control)
                elements.Add((Control)visualAdded);

            if (visualRemoved != null && visualRemoved is Control)
                elements.Remove((Control)visualRemoved);

            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            elements = WPFUtils.FindChildren(this);
        }

        protected void InvokeGotGamepadWindowFocus()
        {
            GotGamepadWindowFocus?.Invoke();
        }

        protected void InvokeLostGamepadWindowFocus()
        {
            LostGamepadWindowFocus?.Invoke();
        }

        #region events
        public event GotGamepadWindowFocusEventHandler GotGamepadWindowFocus;
        public delegate void GotGamepadWindowFocusEventHandler();

        public event LostGamepadWindowFocusEventHandler LostGamepadWindowFocus;
        public delegate void LostGamepadWindowFocusEventHandler();
        #endregion
    }
}
