using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Views.Classes
{
    public class GamepadWindow : Window
    {
        public List<Control> controlElements = new();
        public List<FrameworkElement> frameworkElements = new();

        public ContentDialog currentDialog;
        protected UIGamepad gamepadFocusManager;

        public GamepadWindow()
        {
            LayoutUpdated += OnLayoutUpdated;
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            // Track when objects are added and removed
            if (visualAdded != null && visualAdded is Control)
                controlElements.Add((Control)visualAdded);

            if (visualRemoved != null && visualRemoved is Control)
                controlElements.Remove((Control)visualRemoved);

            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        }

        public ScrollViewer GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer) { return depObj as ScrollViewer; }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null && result.Name.Equals("scrollViewer"))
                    return result;
            }
            return null;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (!this.IsActive || this.Visibility != Visibility.Visible)
                return;

            // get all FrameworkElement(s)
            frameworkElements = WPFUtils.FindChildren(this);

            // do we have a popup ?
            ContentDialog dialog = ContentDialog.GetOpenDialog(this);
            if (dialog is not null)
            {
                if (currentDialog is null)
                {
                    currentDialog = dialog;

                    frameworkElements = WPFUtils.FindChildren(this);

                    // get all Control(s)
                    controlElements = WPFUtils.GetElementsFromPopup<Control>(frameworkElements);

                    ContentDialogOpened?.Invoke(currentDialog);
                }
            }
            else if (dialog is null)
            {
                // get all Control(s)
                controlElements = frameworkElements.OfType<Control>().ToList();

                if (currentDialog is not null)
                {
                    ContentDialogClosed?.Invoke(currentDialog);
                    currentDialog = null;
                }
            }
        }

        protected void InvokeGotGamepadWindowFocus()
        {
            GotGamepadWindowFocus?.Invoke(this);
        }

        protected void InvokeLostGamepadWindowFocus()
        {
            LostGamepadWindowFocus?.Invoke(this);
        }

        #region events
        public event GotGamepadWindowFocusEventHandler GotGamepadWindowFocus;
        public delegate void GotGamepadWindowFocusEventHandler(object sender);

        public event LostGamepadWindowFocusEventHandler LostGamepadWindowFocus;
        public delegate void LostGamepadWindowFocusEventHandler(object sender);

        public event ContentDialogOpenedEventHandler ContentDialogOpened;
        public delegate void ContentDialogOpenedEventHandler(ContentDialog contentDialog);

        public event ContentDialogClosedEventHandler ContentDialogClosed;
        public delegate void ContentDialogClosedEventHandler(ContentDialog contentDialog);
        #endregion
    }
}
