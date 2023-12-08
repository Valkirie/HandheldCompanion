using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HandheldCompanion.Controllers
{
    [Flags]
    public enum ControllerCapabilities : ushort
    {
        None = 0,
        MotionSensor = 1,
        Calibration = 2,
    }

    /// <summary>
    /// Logique d'interaction pour IController.xaml
    /// </summary>
    public partial class IController : UserControl
    {
        // Buttons and axes we should be able to map to.
        // When we have target controllers with different buttons (e.g. in VigEm) this will have to be moved elsewhere.
        public static readonly List<ButtonFlags> TargetButtons = new()
<<<<<<< HEAD
        {
            ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
            ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
            ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
            ButtonFlags.L1, ButtonFlags.R1,
            ButtonFlags.LeftStickClick, ButtonFlags.RightStickClick,
        };

        public static readonly List<AxisLayoutFlags> TargetAxis = new()
        {
            AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
            AxisLayoutFlags.L2, AxisLayoutFlags.R2,
        };
=======
    {
        ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
        ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
        ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
        ButtonFlags.L1, ButtonFlags.R1,
        ButtonFlags.LeftStickClick, ButtonFlags.RightStickClick,
    };

        public static readonly List<AxisLayoutFlags> TargetAxis = new()
    {
        AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
        AxisLayoutFlags.L2, AxisLayoutFlags.R2,
    };
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        public static readonly string defaultGlyph = "\u2753";

        public ControllerCapabilities Capabilities = ControllerCapabilities.None;
        protected SortedDictionary<AxisLayoutFlags, Brush> ColoredAxis = new();

        protected SortedDictionary<ButtonFlags, Brush> ColoredButtons = new();
        protected FontFamily DefaultFontFamily = new("Segeo WP");

        public PnPDetails Details;

        // UI
        protected FontFamily GlyphFontFamily = new("PromptFont");

        public ButtonState InjectedButtons = new();

        public ControllerState Inputs = new();
<<<<<<< HEAD
        public virtual bool IsReady => true;

        public bool IsBusy
        {
            get
            {
                return IsEnabled;
            }
            set
            {
                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsEnabled = !value;
                    ProgressBarPanel.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                });
            }
        }

        protected List<AxisLayoutFlags> SourceAxis = new()
        {
            // same as target, we assume all controllers have those axes
            AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
            AxisLayoutFlags.L2, AxisLayoutFlags.R2
        };
=======
        protected bool isPlugged;

        protected List<AxisLayoutFlags> SourceAxis = new()
    {
        // same as target, we assume all controllers have those axes
        AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
        AxisLayoutFlags.L2, AxisLayoutFlags.R2
    };
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        // Buttons and axes all controllers have that we can map.
        // Additional ones can be added per controller.
        protected List<ButtonFlags> SourceButtons = new()
<<<<<<< HEAD
        {
            // same as target, we assume all controllers have those buttons
            ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
            ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
            ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
            ButtonFlags.L1, ButtonFlags.R1,
            ButtonFlags.LeftStickClick, ButtonFlags.RightStickClick,
            // additional buttons calculated from the above
            ButtonFlags.L2Soft, ButtonFlags.R2Soft, ButtonFlags.L2Full, ButtonFlags.R2Full,
            ButtonFlags.LeftStickUp, ButtonFlags.LeftStickDown, ButtonFlags.LeftStickLeft, ButtonFlags.LeftStickRight,
            ButtonFlags.RightStickUp, ButtonFlags.RightStickDown, ButtonFlags.RightStickLeft, ButtonFlags.RightStickRight
        };

        private byte _UserIndex = 255;
        protected byte UserIndex
        {
            get
            {
                return _UserIndex;
            }
            set
            {
                _UserIndex = value;
                UserIndexChanged?.Invoke(value);

                // UI thread (async)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach(FrameworkElement frameworkElement in UserIndexPanel.Children)
                    {
                        if (frameworkElement is not Border)
                            continue;

                        Border border = (Border)frameworkElement;
                        int idx = UserIndexPanel.Children.IndexOf(border);

                        if (idx == value)
                            border.SetResourceReference(BackgroundProperty, "AccentAAFillColorDefaultBrush");
                        else
                            border.SetResourceReference(BackgroundProperty, "SystemControlForegroundBaseLowBrush");
                    }
                });
            }
        }

=======
    {
        // same as target, we assume all controllers have those buttons
        ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
        ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
        ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
        ButtonFlags.L1, ButtonFlags.R1,
        ButtonFlags.LeftStickClick, ButtonFlags.RightStickClick,
        // additional buttons calculated from the above
        ButtonFlags.L2Soft, ButtonFlags.R2Soft, ButtonFlags.L2Full, ButtonFlags.R2Full,
        ButtonFlags.LeftStickUp, ButtonFlags.LeftStickDown, ButtonFlags.LeftStickLeft, ButtonFlags.LeftStickRight,
        ButtonFlags.RightStickUp, ButtonFlags.RightStickDown, ButtonFlags.RightStickLeft, ButtonFlags.RightStickRight
    };

        protected int UserIndex;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        protected double VibrationStrength = 1.0d;

        public IController()
        {
<<<<<<< HEAD
            InitializeComponent();
        }

        public virtual void AttachDetails(PnPDetails details)
        {
            this.Details = details;
            Details.isHooked = true;
=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        }

        public virtual void UpdateInputs(long ticks)
        {
            InputsUpdated?.Invoke(Inputs);
        }

        public bool HasMotionSensor()
        {
            return Capabilities.HasFlag(ControllerCapabilities.MotionSensor);
        }

<<<<<<< HEAD
        public bool IsPhysical()
        {
            return !IsVirtual();
        }

=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
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

<<<<<<< HEAD
        protected void DrawUI()
        {
            // update name
            ControllerName.Text = (IsVirtual() ? Properties.Resources.Controller_Virtual : string.Empty) + ToString();
=======
        protected void DrawControls()
        {
            // update name
            ui_name.Text = (IsVirtual() ? Properties.Resources.Controller_Virtual : string.Empty) + ToString();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

            // virtual controller shouldn't be visible
            if (IsVirtual())
                this.Visibility = Visibility.Collapsed;
        }

<<<<<<< HEAD
        protected void UpdateUI()
=======
        protected void RefreshControls()
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
<<<<<<< HEAD
                // ui_button_hook.Content = IsPlugged ? Properties.Resources.Controller_Disconnect : Properties.Resources.Controller_Connect;
=======
                if (!IsEnabled)
                    return;

                ui_button_hook.Content = IsPlugged() ? Properties.Resources.Controller_Disconnect : Properties.Resources.Controller_Connect;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
                ui_button_hide.Content = IsHidden() ? Properties.Resources.Controller_Unhide : Properties.Resources.Controller_Hide;
                ui_button_calibrate.Visibility = Capabilities.HasFlag(ControllerCapabilities.Calibration) ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public Button GetButtonHook()
        {
            return ui_button_hook;
        }

        public Button GetButtonHide()
        {
            return ui_button_hide;
        }

        public Button GetButtonCalibrate()
        {
            return ui_button_calibrate;
        }

        public void InjectState(ButtonState State, bool IsKeyDown, bool IsKeyUp)
        {
            if (State.IsEmpty())
                return;

            foreach (var button in State.Buttons)
                InjectedButtons[button] = IsKeyDown;

            LogManager.LogDebug("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", string.Join(',', State.Buttons),
                IsKeyDown, IsKeyUp, ToString());
        }

        public void InjectButton(ButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            if (button == ButtonFlags.None)
                return;

            InjectedButtons[button] = IsKeyDown;

            LogManager.LogDebug("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", button, IsKeyDown, IsKeyUp,
                ToString());
        }

        public virtual void SetVibrationStrength(uint value, bool rumble = false)
        {
            VibrationStrength = value / 100.0d;
            if (rumble) Rumble();
        }

        public virtual void SetVibration(byte LargeMotor, byte SmallMotor)
        {
        }

        // let the controller decide itself what motor to use for a specific button
        public virtual void SetHaptic(HapticStrength strength, ButtonFlags button)
<<<<<<< HEAD
        {
            int delay;
            switch (strength)
            {
                default:
                case HapticStrength.Low:
                    delay = 85;
                    break;

                case HapticStrength.Medium:
                    delay = 105;
                    break;

                case HapticStrength.High:
                    delay = 125;
                    break;
            }

            switch (button)
            {
                case ButtonFlags.B1:
                case ButtonFlags.B2:
                case ButtonFlags.B3:
                case ButtonFlags.B4:
                case ButtonFlags.L1:
                case ButtonFlags.L2Soft:
                case ButtonFlags.Start:
                case ButtonFlags.RightStickClick:
                case ButtonFlags.RightPadClick:
                    Rumble(delay, 0, byte.MaxValue);
                    break;
                default:
                    Rumble(delay, byte.MaxValue, 0);
                    break;
            }
        }
=======
        { }
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        public virtual bool IsConnected()
        {
            return false;
        }

<<<<<<< HEAD
        private Task rumbleTask;
        public virtual void Rumble(int delay = 125, byte LargeMotor = byte.MaxValue, byte SmallMotor = byte.MaxValue)
        {
            // If the current task is not null and not completed
            if (rumbleTask != null && !rumbleTask.IsCompleted)
                SetVibration(0, 0);

            // Create a new task that executes the following code
            rumbleTask = Task.Run(async () =>
            {
                SetVibration(LargeMotor, SmallMotor);
=======
        public virtual void Rumble(int delay = 125)
        {
            Task.Run(async () =>
            {
                SetVibration(byte.MaxValue, byte.MaxValue);
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
                await Task.Delay(delay);
                SetVibration(0, 0);
            });
        }

<<<<<<< HEAD
=======
        public virtual bool IsPlugged()
        {
            return isPlugged;
        }

>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        // this function cannot be called twice
        public virtual void Plug()
        {
            SetVibrationStrength(SettingsManager.GetUInt("VibrationStrength"));

<<<<<<< HEAD
            InjectedButtons.Clear();

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ui_button_hook.IsEnabled = false;
            });
=======
            isPlugged = true;

            InjectedButtons.Clear();

            RefreshControls();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        }

        // this function cannot be called twice
        public virtual void Unplug()
        {
<<<<<<< HEAD
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                ui_button_hook.IsEnabled = true;
            });
=======
            isPlugged = false;

            RefreshControls();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        }

        // like Unplug but one that can be safely called when controller is already removed
        public virtual void Cleanup()
        {
        }

        public bool IsHidden()
        {
<<<<<<< HEAD
            // bool hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            bool hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
=======
            // var hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            var hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
            return /* hide_device || */ hide_base;
        }

        public virtual void Hide(bool powerCycle = true)
        {
            HideHID();

            if (powerCycle)
            {
<<<<<<< HEAD
                IsBusy = true;

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;
                CyclePort();
            }

            UpdateUI();
=======
                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsEnabled = false;
                    ProgressBarPanel.Visibility = Visibility.Visible;
                });

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;

                CyclePort();
                return;
            }

            RefreshControls();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        }

        public virtual void Unhide(bool powerCycle = true)
        {
            UnhideHID();

            if (powerCycle)
            {
<<<<<<< HEAD
                IsBusy = true;

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;
                CyclePort();
            }

            UpdateUI();
=======
                // UI thread (async)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsEnabled = false;
                    ProgressBarPanel.Visibility = Visibility.Visible;
                });

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;

                CyclePort();
                return;
            }

            RefreshControls();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        }

        public virtual void CyclePort()
        {
            Details.CyclePort();
        }

        public virtual void SetLightColor(byte R, byte G, byte B)
        {
        }

<<<<<<< HEAD
        protected void HideHID()
=======
        public void HideHID()
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        {
            HidHide.HidePath(Details.baseContainerDeviceInstanceId);
            HidHide.HidePath(Details.deviceInstanceId);

            /*
            // get HidHideDevice
            HidHideDevice hideDevice = HidHide.GetHidHideDevice(Details.baseContainerDeviceInstanceId);
            if (hideDevice is not null)
                foreach (HidHideSubDevice subDevice in hideDevice.Devices)
                    HidHide.HidePath(subDevice.DeviceInstancePath);
            */
        }

<<<<<<< HEAD
        protected void UnhideHID()
=======
        public void UnhideHID()
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        {
            HidHide.UnhidePath(Details.baseContainerDeviceInstanceId);
            HidHide.UnhidePath(Details.deviceInstanceId);

            /*
            // get HidHideDevice
            HidHideDevice hideDevice = HidHide.GetHidHideDevice(Details.baseContainerDeviceInstanceId);
            if (hideDevice is not null)
                foreach (HidHideSubDevice subDevice in hideDevice.Devices)
                    HidHide.UnhidePath(subDevice.DeviceInstancePath);
            */
        }

        public virtual bool RestoreDrivers()
        {
            return true;
        }

        protected virtual void Calibrate()
        {
        }

        protected virtual void ui_button_calibrate_Click(object sender, RoutedEventArgs e)
        {
            CalibrateClicked?.Invoke(this);
        }

        protected virtual void ui_button_hide_Click(object sender, RoutedEventArgs e)
        {
            HideClicked?.Invoke(this);
        }

        protected virtual void ui_button_hook_Click(object sender, RoutedEventArgs e)
        {
            HookClicked?.Invoke(this);
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
                case ButtonFlags.LeftStickClick:
                    return "\u21BA";
                case ButtonFlags.RightStickClick:
                    return "\u21BB";
                case ButtonFlags.LeftStickUp:
                    return "\u21BE";
                case ButtonFlags.LeftStickDown:
                    return "\u21C2";
                case ButtonFlags.LeftStickLeft:
                    return "\u21BC";
                case ButtonFlags.LeftStickRight:
                    return "\u21C0";
                case ButtonFlags.RightStickUp:
                    return "\u21BF";
                case ButtonFlags.RightStickDown:
                    return "\u21C3";
                case ButtonFlags.RightStickLeft:
                    return "\u21BD";
                case ButtonFlags.RightStickRight:
                    return "\u21C1";
<<<<<<< HEAD
=======
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
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
                case ButtonFlags.VolumeUp:
                    return "\u21fe";
                case ButtonFlags.VolumeDown:
                    return "\u21fd";
            }

            return defaultGlyph;
        }

        public virtual string GetGlyph(AxisFlags axis)
        {
            switch (axis)
            {
                case AxisFlags.LeftStickX:
                    return "\u21C4";
                case AxisFlags.LeftStickY:
                    return "\u21C5";
                case AxisFlags.RightStickX:
                    return "\u21C6";
                case AxisFlags.RightStickY:
                    return "\u21F5";
            }

            return defaultGlyph;
        }

        public virtual string GetGlyph(AxisLayoutFlags axis)
        {
            switch (axis)
            {
                case AxisLayoutFlags.LeftStick:
                    return "\u21CB";
                case AxisLayoutFlags.RightStick:
                    return "\u21CC";
                case AxisLayoutFlags.Gyroscope:
                    return "\u2B94";
            }

            return defaultGlyph;
        }

        public FontIcon GetFontIcon(ButtonFlags button, int FontIconSize = 14)
        {
            var FontIcon = new FontIcon
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
            var FontIcon = new FontIcon
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
            if (ColoredButtons.TryGetValue(button, out var brush))
                return brush;

            return null;
        }

        public Brush GetGlyphColor(AxisLayoutFlags axis)
        {
            /* if (AxisBrush.TryGetValue(axis, out Brush brush))
                return brush; */

            return null;
        }

        private static bool IsTrigger(AxisLayoutFlags axis)
        {
            return axis is AxisLayoutFlags.L2 || axis is AxisLayoutFlags.R2;
        }

        public static IEnumerable<ButtonFlags> GetTargetButtons()
        {
            var buttons = Enum.GetValues(typeof(ButtonFlags)).Cast<ButtonFlags>();

            return buttons.Where(a => TargetButtons.Contains(a));
        }

        public static IEnumerable<AxisLayoutFlags> GetTargetAxis()
        {
            var axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => TargetAxis.Contains(a) && !IsTrigger(a));
        }

        public static IEnumerable<AxisLayoutFlags> GetTargetTriggers()
        {
            var axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => TargetAxis.Contains(a) && IsTrigger(a));
        }

        public bool HasSourceButton(ButtonFlags button)
        {
            return SourceButtons.Contains(button);
        }

        public bool HasSourceAxis(AxisLayoutFlags axis)
        {
            return SourceAxis.Contains(axis);
        }

        public string GetButtonName(ButtonFlags button)
        {
            return EnumUtils.GetDescriptionFromEnumValue(button, GetType().Name);
        }

        public string GetAxisName(AxisLayoutFlags axis)
        {
            return EnumUtils.GetDescriptionFromEnumValue(axis, GetType().Name);
        }

        #region events

<<<<<<< HEAD
        public event UserIndexChangedEventHandler UserIndexChanged;
        public delegate void UserIndexChangedEventHandler(byte UserIndex);

=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
        public event InputsUpdatedEventHandler InputsUpdated;
        public delegate void InputsUpdatedEventHandler(ControllerState Inputs);

        public event HookClickedEventHandler HookClicked;
        public delegate void HookClickedEventHandler(IController controller);

        public event HideClickedEventHandler HideClicked;
        public delegate void HideClickedEventHandler(IController controller);

        public event CalibrateClickedEventHandler CalibrateClicked;
        public delegate void CalibrateClickedEventHandler(IController controller);

        #endregion
    }
}
