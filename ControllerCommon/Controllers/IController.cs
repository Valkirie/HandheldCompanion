using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using ModernWpf.Controls;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControllerCommon.Controllers
{
    [Flags]
    public enum ControllerCapacities : ushort
    {
        None = 0,
        Gyroscope = 1,
        Accelerometer = 2,
        Trackpad = 3,
    }

    public abstract class IController
    {
        #region events
        public event InputsUpdatedEventHandler InputsUpdated;
        public delegate void InputsUpdatedEventHandler(ControllerState Inputs);

        public event MovementsUpdatedEventHandler MovementsUpdated;
        public delegate void MovementsUpdatedEventHandler(ControllerMovements Movements);
        #endregion

        public ControllerState Inputs = new();
        public ControllerMovements Movements = new();

        public static List<ButtonFlags> VirtualButtons = new()
        {
            ButtonFlags.None,
            ButtonFlags.L2, ButtonFlags.R2, ButtonFlags.L3, ButtonFlags.R3,ButtonFlags.L4, ButtonFlags.R4, ButtonFlags.L5, ButtonFlags.R5,
            ButtonFlags.LeftThumbUp, ButtonFlags.LeftThumbDown, ButtonFlags.LeftThumbLeft, ButtonFlags.LeftThumbRight,
            ButtonFlags.RightThumbUp, ButtonFlags.RightThumbDown, ButtonFlags.RightThumbLeft, ButtonFlags.RightThumbRight,
            ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight,
            ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight,
            ButtonFlags.OEM1, ButtonFlags.OEM2, ButtonFlags.OEM3, ButtonFlags.OEM4, ButtonFlags.OEM5, ButtonFlags.OEM6, ButtonFlags.OEM7, ButtonFlags.OEM8, ButtonFlags.OEM9, ButtonFlags.OEM10,
        };

        public static List<AxisLayoutFlags> VirtualAxis = new()
        {
            AxisLayoutFlags.None,
            AxisLayoutFlags.LeftPad, AxisLayoutFlags.RightPad
        };

        protected List<ButtonFlags> SupportedButtons = new()
        {
            ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
            ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
            ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
            ButtonFlags.L1, ButtonFlags.R1, ButtonFlags.L2, ButtonFlags.R2, ButtonFlags.L3, ButtonFlags.R3,
            ButtonFlags.LeftThumb, ButtonFlags.RightThumb,
            ButtonFlags.LeftThumbUp, ButtonFlags.LeftThumbDown, ButtonFlags.LeftThumbLeft, ButtonFlags.LeftThumbRight,
            ButtonFlags.RightThumbUp, ButtonFlags.RightThumbDown, ButtonFlags.RightThumbLeft, ButtonFlags.RightThumbRight,
        };

        protected List<AxisLayoutFlags> SupportedAxis = new()
        {
            AxisLayoutFlags.LeftThumb, AxisLayoutFlags.RightThumb,
            AxisLayoutFlags.L2, AxisLayoutFlags.R2,
        };

        protected Dictionary<ButtonFlags, Brush> ColoredButtons = new();
        protected Dictionary<AxisLayoutFlags, Brush> ColoredAxis = new();

        protected const short UPDATE_INTERVAL = 10;

        public ButtonState InjectedButtons = new();
        public ButtonState prevInjectedButtons = new();

        public ControllerCapacities Capacities = ControllerCapacities.None;

        protected int UserIndex;
        protected double VibrationStrength = 1.0d;

        public PnPDetails Details;

        protected PrecisionTimer MovementsTimer;
        protected PrecisionTimer InputsTimer;

        // UI
        protected FontFamily GlyphFontFamily = new("PromptFont");
        protected FontFamily DefaultFontFamily = new("Segeo WP");

        protected Border ui_border = new Border() { CornerRadius = new CornerRadius(4, 4, 4, 4), Padding = new Thickness(15, 12, 12, 12) };
        protected Grid ui_grid = new Grid();
        protected FontIcon ui_icon = new FontIcon() { Glyph = "\uE7FC", Height = 40, HorizontalAlignment = HorizontalAlignment.Center };
        protected TextBlock ui_name = new TextBlock() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        protected Button ui_button_hide = new Button() { Width = 100, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        protected Button ui_button_hook = new Button() { Width = 100, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Style = Application.Current.FindResource("AccentButtonStyle") as Style };
        protected DockPanel ui_dock_content = new DockPanel() { HorizontalAlignment = HorizontalAlignment.Left };
        protected SimpleStackPanel ui_dock_buttons = new SimpleStackPanel() { Spacing = 6, Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        protected IController()
        {
            InputsTimer = new PrecisionTimer();
            InputsTimer.SetInterval(UPDATE_INTERVAL);

            MovementsTimer = new PrecisionTimer();
            MovementsTimer.SetInterval(UPDATE_INTERVAL);
            MovementsTimer.SetAutoResetMode(true);

            // attribute controller to tag
            ui_border.Tag = this;
        }

        public virtual void UpdateInputs()
        {
            // update states
            Inputs.Timestamp = Environment.TickCount;
            prevInjectedButtons = InjectedButtons.Clone() as ButtonState;

            InputsUpdated?.Invoke(Inputs);
        }

        public virtual void UpdateMovements()
        {
            // update states
            Movements.Timestamp = Environment.TickCount;

            MovementsUpdated?.Invoke(Movements);
        }

        public bool HasGyro()
        {
            return Capacities.HasFlag(ControllerCapacities.Gyroscope);
        }

        public bool HasAccelerometer()
        {
            return Capacities.HasFlag(ControllerCapacities.Accelerometer);
        }

        public bool HasTrackpad()
        {
            return Capacities.HasFlag(ControllerCapacities.Trackpad);
        }

        public bool IsVirtual()
        {
            if (Details is not null)
                return Details.isVirtual;
            return true;
        }

        public bool IsGaming()
        {
            if (Details is not null)
                return Details.isGaming;
            return false;
        }

        public int GetUserIndex()
        {
            return UserIndex;
        }

        public string GetInstancePath()
        {
            if (Details is not null)
                return Details.deviceInstanceId;
            return string.Empty;
        }

        public string GetContainerInstancePath()
        {
            if (Details is not null)
                return Details.baseContainerDeviceInstanceId;
            return string.Empty;
        }

        public override string ToString()
        {
            if (Details is not null)
                return Details.Name;
            return string.Empty;
        }

        protected void DrawControls()
        {
            // update name
            ui_name.Text = this.ToString();

            // Define columns
            ColumnDefinition colDef0 = new ColumnDefinition() { Width = new GridLength(9, GridUnitType.Star), MinWidth = 200 };
            ColumnDefinition colDef1 = new ColumnDefinition() { MinWidth = 240 };

            ui_grid.ColumnDefinitions.Add(colDef0);
            ui_grid.ColumnDefinitions.Add(colDef1);

            // SetResourceReference
            /*
            ui_icon.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_name.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_button_hide.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            ui_button_hook.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseHighBrush");
            */
            ui_border.SetResourceReference(Control.BackgroundProperty, "SystemControlPageBackgroundAltHighBrush");

            ui_dock_content.Children.Add(ui_icon);
            ui_dock_content.Children.Add(ui_name);
            ui_grid.Children.Add(ui_dock_content);
            Grid.SetColumn(ui_dock_content, 0);

            ui_dock_buttons.Children.Add(ui_button_hook);
            ui_dock_buttons.Children.Add(ui_button_hide);
            ui_grid.Children.Add(ui_dock_buttons);
            Grid.SetColumn(ui_dock_buttons, 1);

            ui_border.Child = ui_grid;
        }

        protected void RefreshControls()
        {
            ui_button_hook.IsEnabled = !IsPlugged();
            ui_button_hook.Content = IsPlugged() ? "Connected" : "Connect";
            ui_button_hide.Content = IsHidden() ? "Unhide" : "Hide";
        }

        public FrameworkElement GetControl()
        {
            return ui_border;
        }

        public Button GetButtonHook()
        {
            return ui_button_hook;
        }

        public Button GetButtonHide()
        {
            return ui_button_hide;
        }

        public void InjectButton(ButtonState State, bool IsKeyDown, bool IsKeyUp)
        {
            if (State.IsEmpty())
                return;

            foreach (var button in State.Buttons)
                InjectedButtons[button] = IsKeyDown;

            LogManager.LogDebug("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", State, IsKeyDown, IsKeyUp, ToString());
        }

        public virtual void SetVibrationStrength(double value)
        {
            VibrationStrength = value / 100;
        }

        public virtual void SetVibration(byte LargeMotor, byte SmallMotor)
        { }

        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual void Rumble(int loop)
        { }

        public virtual bool IsPlugged()
        {
            return InputsTimer.IsRunning();
        }

        public virtual void Plug()
        {
            InjectedButtons.Clear();

            InputsTimer.SetAutoResetMode(true);
            InputsTimer.Start();

            RefreshControls();
        }

        public virtual void Unplug()
        {
            InputsTimer.Stop();

            RefreshControls();
        }

        public bool IsHidden()
        {
            bool hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            bool hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
            return (hide_device || hide_base);
        }

        public void Hide()
        {
            HidHide.HidePath(Details.deviceInstanceId);
            HidHide.HidePath(Details.baseContainerDeviceInstanceId);

            RefreshControls();
        }

        public void Unhide()
        {
            HidHide.UnhidePath(Details.deviceInstanceId);
            HidHide.UnhidePath(Details.baseContainerDeviceInstanceId);

            RefreshControls();
        }

        public virtual string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.DPadUp:
                    return "\u219F"; // Button A
                case ButtonFlags.DPadDown:
                    return "\u21A1"; // Button B
                case ButtonFlags.DPadLeft:
                    return "\u219E"; // Button X
                case ButtonFlags.DPadRight:
                    return "\u21A0"; // Button Y
                case ButtonFlags.LeftThumb:
                    return "\u21BA";
                case ButtonFlags.RightThumb:
                    return "\u21BB";
                case ButtonFlags.LeftThumbUp:
                    return "\u21BE";
                case ButtonFlags.LeftThumbDown:
                    return "\u21C2";
                case ButtonFlags.LeftThumbLeft:
                    return "\u21BC";
                case ButtonFlags.LeftThumbRight:
                    return "\u21C0";
                case ButtonFlags.RightThumbUp:
                    return "\u21BF";
                case ButtonFlags.RightThumbDown:
                    return "\u21C3";
                case ButtonFlags.RightThumbLeft:
                    return "\u21BD";
                case ButtonFlags.RightThumbRight:
                    return "\u21C1";
                case ButtonFlags.OEM1:
                    return "\u2780";
                case ButtonFlags.OEM2:
                    return "\u2781";
                case ButtonFlags.OEM3:
                    return "\u2782";
                case ButtonFlags.OEM4:
                    return "\u2783";
                case ButtonFlags.OEM5:
                    return "\u2784";
                case ButtonFlags.OEM6:
                    return "\u2785";
                case ButtonFlags.OEM7:
                    return "\u2786";
                case ButtonFlags.OEM8:
                    return "\u2787";
                case ButtonFlags.OEM9:
                    return "\u2788";
                case ButtonFlags.OEM10:
                    return "\u2789";
            }
            return "\u2753";
        }

        public virtual string GetGlyph(AxisFlags axis)
        {
            switch (axis)
            {
                case AxisFlags.LeftThumbX:
                    return "\u21C4";
                case AxisFlags.LeftThumbY:
                    return "\u21C5";
                case AxisFlags.RightThumbX:
                    return "\u21C6";
                case AxisFlags.RightThumbY:
                    return "\u21F5";
            }
            return "\u2753";
        }

        public virtual string GetGlyph(AxisLayoutFlags axis)
        {
            switch (axis)
            {
                case AxisLayoutFlags.LeftThumb:
                    return "\u21CB";
                case AxisLayoutFlags.RightThumb:
                    return "\u21CC";
            }
            return "\u2753";
        }

        public FontIcon GetFontIcon(ButtonFlags button, int FontIconSize = 14)
        {
            FontIcon FontIcon = new FontIcon()
            {
                Glyph = GetGlyph(button),
                FontSize = FontIconSize,
                Foreground = GetGlyphColor(button)
            };

            if (FontIcon.Glyph is not null)
            {
                FontIcon.FontFamily = GlyphFontFamily;
                FontIcon.FontSize = 28;
            }

            return FontIcon;
        }

        public FontIcon GetFontIcon(AxisLayoutFlags axis, int FontIconSize = 14)
        {
            FontIcon FontIcon = new FontIcon()
            {
                Glyph = GetGlyph(axis),
                FontSize = FontIconSize,
                Foreground = GetGlyphColor(axis)
            };

            if (FontIcon.Glyph is not null)
            {
                FontIcon.FontFamily = GlyphFontFamily;
                FontIcon.FontSize = 28;
            }

            return FontIcon;
        }

        public Brush GetGlyphColor(ButtonFlags button)
        {
            if (ColoredButtons.ContainsKey(button))
                return ColoredButtons[button];

            return null;
        }

        public Brush GetGlyphColor(AxisLayoutFlags axis)
        {
            /* if (AxisBrush.ContainsKey(axis))
                return AxisBrush[axis]; */

            return null;
        }

        public IEnumerable<ButtonFlags> GetButtons()
        {
            IEnumerable<ButtonFlags> buttons = Enum.GetValues(typeof(ButtonFlags)).Cast<ButtonFlags>();

            return buttons.Where(a => IsButtonSupported(a) && !IsButtonBlacklisted(a));
        }

        public IEnumerable<AxisLayoutFlags> GetAxis()
        {
            IEnumerable<AxisLayoutFlags> axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => IsAxisSupported(a) && !IsAxisBlacklisted(a) && !IsAxisTrigger(a));
        }

        public IEnumerable<AxisLayoutFlags> GetTriggers()
        {
            IEnumerable<AxisLayoutFlags> axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => IsAxisSupported(a) && !IsAxisBlacklisted(a) && IsAxisTrigger(a));
        }

        public bool IsButtonBlacklisted(ButtonFlags button)
        {
            return VirtualButtons.Contains(button);
        }

        public bool IsAxisBlacklisted(AxisLayoutFlags axis)
        {
            return VirtualAxis.Contains(axis);
        }

        public bool IsButtonSupported(ButtonFlags button)
        {
            return SupportedButtons.Contains(button);
        }

        public bool IsAxisSupported(AxisLayoutFlags axis)
        {
            return SupportedAxis.Contains(axis);
        }

        public bool IsAxisTrigger(AxisLayoutFlags axis)
        {
            return axis is AxisLayoutFlags.L2 || axis is AxisLayoutFlags.R2;
        }

        public string GetButtonName(ButtonFlags button)
        {
            return EnumUtils.GetDescriptionFromEnumValue(button, this.GetType().Name);
        }

        public string GetAxisName(AxisLayoutFlags axis)
        {
            return EnumUtils.GetDescriptionFromEnumValue(axis, this.GetType().Name);
        }
    }
}
