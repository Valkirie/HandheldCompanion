using HandheldCompanion.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace HandheldCompanion.Views.Classes
{
    /// <summary>
    /// Speaks gamepad focus changes through the running screen reader for windows that never become the
    /// foreground window (QuickTools uses WS_EX_NOACTIVATE), as screen readers ignore UI Automation focus
    /// events coming from background windows. Foreground windows are left to the screen reader itself.
    /// </summary>
    public sealed class ScreenReaderAnnouncer
    {
        private static readonly DependencyProperty[] ValueProperties =
        [
            ToggleSwitch.IsOnProperty,
            ToggleButton.IsCheckedProperty,
            RangeBase.ValueProperty,
            Selector.SelectedItemProperty,
            NumberBox.ValueProperty,
            Expander.IsExpandedProperty,
            SettingsExpander.IsExpandedProperty,
            MenuItem.IsCheckedProperty,
            UIElement.IsEnabledProperty,
        ];

        private readonly GamepadWindow window;
        private readonly DispatcherTimer pendingTimer;
        private readonly List<(DependencyPropertyDescriptor Descriptor, Control Control)> hooks = [];

        private Control? current;
        private object? currentModal;
        private string? pendingPrefix;

        public ScreenReaderAnnouncer(GamepadWindow window)
        {
            this.window = window;

            // gives gamepad focus a moment to settle, so the prefix is spoken together with the focused control
            pendingTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.FromMilliseconds(400) };
            pendingTimer.Tick += PendingTimer_Tick;
        }

        private bool ShouldSpeak
        {
            get
            {
                if (!window.IsVisible || !ScreenReader.IsActive)
                    return false;

                // the screen reader follows UI Automation focus in the foreground window by itself
                nint handle = window.hwndSource?.Handle ?? 0;
                return handle == 0 || WinAPI.GetForegroundWindow() != handle;
            }
        }

        public void FocusChanged(Control control)
        {
            if (ReferenceEquals(control, current) || !ShouldSpeak)
                return;

            Attach(control);

            string text = Combine(pendingPrefix, ModalContext(), AccessibilityHelper.Describe(control));
            pendingPrefix = null;
            pendingTimer.Stop();

            ScreenReader.Speak(text);
        }

        /// <summary>Announces a window/page change, merged with the next focused control.</summary>
        public void Announce(string text)
        {
            Detach();
            pendingPrefix = text;
            pendingTimer.Stop();
            pendingTimer.Start();
        }

        /// <summary>Announces immediately, e.g. when the window gets hidden.</summary>
        public void AnnounceNow(string text)
        {
            Detach();
            pendingPrefix = null;
            pendingTimer.Stop();
            currentModal = null;

            ScreenReader.Speak(text);
        }

        private void PendingTimer_Tick(object? sender, EventArgs e)
        {
            pendingTimer.Stop();
            if (pendingPrefix is null || !ShouldSpeak)
                return;

            // focus did not move (restored to the same control): say where we are
            Control? focused = window.GetFocusedElement();
            string text = pendingPrefix;
            pendingPrefix = null;

            if (focused is not null && focused.IsVisible)
            {
                Attach(focused);
                text = Combine(text, AccessibilityHelper.Describe(focused));
            }

            ScreenReader.Speak(text);
        }

        /// <summary>Title and body of a dialog, or the name of a menu, the first time focus lands in it.</summary>
        private string? ModalContext()
        {
            object? modal = (object?)window.currentDialog ?? (object?)window.currentMessageBox ?? (object?)window.currentFlyoutButton ?? window.currentFlyout;
            if (ReferenceEquals(modal, currentModal))
                return null;

            currentModal = modal;
            return modal switch
            {
                ContentDialog dialog => Combine(AccessibilityHelper.Text(dialog.Title), AccessibilityHelper.StaticText(dialog.Content), "dialog"),
                MessageBox messageBox => Combine(AccessibilityHelper.Text(messageBox.Caption), AccessibilityHelper.StaticText(messageBox.Content), "dialog"),
                DropDownButton button => Combine(AccessibilityHelper.Describe(button).Split(',')[0], "menu"),
                FlyoutBase => "menu",
                _ => null,
            };
        }

        #region value changes

        private void Attach(Control control)
        {
            Detach();
            current = control;

            foreach (DependencyProperty property in ValueProperties)
            {
                if (!property.OwnerType.IsInstanceOfType(control) && property != UIElement.IsEnabledProperty)
                    continue;

                DependencyPropertyDescriptor? descriptor = DependencyPropertyDescriptor.FromProperty(property, control.GetType());
                if (descriptor is null)
                    continue;

                descriptor.AddValueChanged(control, Control_ValueChanged);
                hooks.Add((descriptor, control));
            }
        }

        private void Detach()
        {
            foreach ((DependencyPropertyDescriptor descriptor, Control control) in hooks)
                descriptor.RemoveValueChanged(control, Control_ValueChanged);

            hooks.Clear();
            current = null;
        }

        private void Control_ValueChanged(object? sender, EventArgs e)
        {
            if (sender is not Control control || !ReferenceEquals(control, current) || !ShouldSpeak)
                return;

            string state = AccessibilityHelper.DescribeState(control);
            if (!control.IsEnabled)
                state = Combine(state, "unavailable");

            ScreenReader.Speak(state);
        }

        #endregion

        private static string Combine(params string?[] parts)
        {
            List<string> values = [];
            foreach (string? part in parts)
                if (!string.IsNullOrWhiteSpace(part))
                    values.Add(part.Trim());

            return string.Join(", ", values);
        }
    }
}
