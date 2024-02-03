﻿using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

                    ContentDialogOpened?.Invoke();
                }
            }
            else if (dialog is null)
            {
                // get all Control(s)
                controlElements = frameworkElements.OfType<Control>().ToList();

                if (currentDialog is not null)
                {
                    currentDialog = null;
                }
            }
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

        public event ContentDialogOpenedEventHandler ContentDialogOpened;
        public delegate void ContentDialogOpenedEventHandler();

        public event ContentDialogClosedEventHandler ContentDialogClosed;
        public delegate void ContentDialogClosedEventHandler();
        #endregion
    }
}
