using HandheldCompanion.Actions;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    }

    /// <summary>
    /// Logique d'interaction pour IController.xaml
    /// </summary>
    public partial class IController : UserControl
    {
        // Buttons and axes we should be able to map to.
        // When we have target controllers with different buttons (e.g. in VigEm) this will have to be moved elsewhere.
        protected readonly List<ButtonFlags> TargetButtons =
        [
            ButtonFlags.None, ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4,
            ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight,
            ButtonFlags.Start, ButtonFlags.Back, ButtonFlags.Special,
            ButtonFlags.L1, ButtonFlags.R1,
            ButtonFlags.LeftStickClick, ButtonFlags.RightStickClick,
        ];

        protected readonly List<AxisLayoutFlags> TargetAxis =
        [
            AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
            AxisLayoutFlags.L2, AxisLayoutFlags.R2,
        ];

        protected readonly List<AxisLayoutFlags> SourceAxis =
        [
            // same as target, we assume all controllers have those axes
            AxisLayoutFlags.LeftStick, AxisLayoutFlags.RightStick,
            AxisLayoutFlags.L2, AxisLayoutFlags.R2
        ];

        // Buttons and axes all controllers have that we can map.
        // Additional ones can be added per controller.
        protected readonly List<ButtonFlags> SourceButtons =
        [
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
        ];

        protected FontFamily GlyphFontFamily = new("PromptFont");
        public static readonly string defaultGlyph = "\u2753";
        public ControllerCapabilities Capabilities = ControllerCapabilities.None;
        protected SortedDictionary<AxisLayoutFlags, Brush> ColoredAxis = [];
        protected SortedDictionary<ButtonFlags, Brush> ColoredButtons = [];
        protected FontFamily DefaultFontFamily = new("Segeo WP");

        public PnPDetails Details;

        public ButtonState InjectedButtons = new();
        public ControllerState Inputs = new();

        protected byte gamepadIndex = 0;
        protected Dictionary<byte, GamepadMotion> gamepadMotions = new Dictionary<byte, GamepadMotion>
        {
            {0, new(string.Empty, CalibrationMode.Manual) },
            {1, new(string.Empty, CalibrationMode.Manual) },
        };

        protected double VibrationStrength = 1.0d;
        private byte _UserIndex = 255;
        private readonly int MaxUserIndex = 10;

        private int workingIdx = 0;
        private Thread workingThread;
        private bool workingThreadRunning;

        protected object hidLock = new();

        public virtual bool IsReady => true;
        public virtual bool IsWireless => false;
        public bool isPlaceholder;

        public bool IsBusy
        {
            get
            {
                bool isBusy = false;

                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isBusy = !IsEnabled;
                });

                return isBusy;
            }

            set
            {
                // UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsEnabled = !value;
                    ProgressBarWarning.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                });

                switch (value)
                {
                    case false:
                        {
                            // kill working thread
                            if (workingThread is not null)
                            {
                                workingThreadRunning = false;
                                // Ensure the thread has finished execution
                                if (workingThread.IsAlive)
                                    workingThread.Join();
                                workingThread = null;
                            }

                            // visually update user index
                            SetVirtualControllerVisualIndex(UserIndex);
                        }
                        break;

                    case true:
                        {
                            workingThreadRunning = true;
                            workingThread = new Thread(workingThreadLoop)
                            {
                                IsBackground = true,
                            };
                            workingThread.Start();
                        }
                        return;
                }
            }
        }

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

                if (IsBusy)
                    return;

                // visually update user index
                SetVirtualControllerVisualIndex(value);
            }
        }

        private void SetVirtualControllerVisualIndex(int value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (FrameworkElement frameworkElement in UserIndexPanel.Children)
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

        private void workingThreadLoop()
        {
            int direction = 1; // 1 for increasing, -1 for decreasing
            workingIdx = 0;

            while (workingThreadRunning)
            {
                workingIdx += direction; // increment or decrement the index
                if (workingIdx == MaxUserIndex - 1 || workingIdx == 0) // if the index reaches the limit or zero
                    direction = -direction; // reverse the direction

                SetVirtualControllerVisualIndex(workingIdx);

                Thread.Sleep(100);
            }
        }

        public IController()
        {
            InitializeComponent();
            InitializeInputOutput();

            MaxUserIndex = UserIndexPanel.Children.Count;
        }

        protected virtual void UpdateSettings()
        { }

        protected virtual void InitializeInputOutput()
        { }

        public virtual void AttachDetails(PnPDetails details)
        {
            if (details is null)
                return;

            this.Details = details;
            Details.isHooked = true;

            if (details.isVirtual)
                return;

            // manage gamepad motion
            gamepadMotions[0] = new(details.deviceInstanceId, CalibrationMode.Manual | CalibrationMode.SensorFusion);

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                ControllerType.Glyph = details.isInternal ? "\uE990" : details.isBluetooth ? "\uE702" : "\uECF0";
            });

            /*
            // Retrieve the oldest device using LINQ
            PnPDetails oldest = Details.isXInput ? DeviceManager.GetOldestXInput() : DeviceManager.GetOldestDInput();
            if (oldest is not null)
                IsInternal = oldest.deviceInstanceId == Details.deviceInstanceId;
            */
        }

        public virtual void UpdateInputs(long ticks, float delta)
        {
            InputsUpdated?.Invoke(Inputs, gamepadMotions, delta, gamepadIndex);
        }

        public bool HasMotionSensor()
        {
            return Capabilities.HasFlag(ControllerCapabilities.MotionSensor);
        }

        public GamepadMotion GetMotionSensor()
        {
            return gamepadMotions[gamepadIndex];
        }

        public bool IsPhysical()
        {
            return !IsVirtual();
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

        protected void DrawUI()
        {
            // update name
            ControllerName.Text = (IsVirtual() ? Properties.Resources.Controller_Virtual : string.Empty) + ToString();

            // some elements of virtual controllers shouldn't be visible
            if (IsVirtual())
            {
                ui_button_hook.Visibility = Visibility.Collapsed;
                ui_button_hide.Visibility = Visibility.Collapsed;
                ui_button_calibrate.Visibility = Visibility.Collapsed;
            }
        }

        protected void UpdateUI()
        {
            // some elements of virtual controllers shouldn't be visible
            if (IsVirtual())
                return;

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // ui_button_hook.Content = IsPlugged ? Properties.Resources.Controller_Disconnect : Properties.Resources.Controller_Connect;
                ui_button_hide.Content = IsHidden() ? Properties.Resources.Controller_Unhide : Properties.Resources.Controller_Hide;
                ui_button_calibrate.Visibility = Capabilities.HasFlag(ControllerCapabilities.MotionSensor) ? Visibility.Visible : Visibility.Collapsed;
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

            LogManager.LogTrace("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", string.Join(',', State.Buttons),
                IsKeyDown, IsKeyUp, ToString());
        }

        public void InjectButton(ButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            if (button == ButtonFlags.None)
                return;

            InjectedButtons[button] = IsKeyDown;

            LogManager.LogTrace("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", button, IsKeyDown, IsKeyUp,
                ToString());
        }

        public virtual void SetVibrationStrength(uint value, bool rumble = false)
        {
            VibrationStrength = value / 100.0d;
            if (rumble) Rumble();
        }

        public virtual void SetVibration(byte LargeMotor, byte SmallMotor)
        { }

        // let the controller decide itself what motor to use for a specific button
        public virtual void SetHaptic(HapticStrength strength, ButtonFlags button)
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

        public virtual bool IsConnected()
        {
            return false;
        }

        private Task rumbleTask;
        public virtual void Rumble(int delay = 125, byte LargeMotor = byte.MaxValue, byte SmallMotor = byte.MaxValue)
        {
            // If the current task is not null and not completed
            if (rumbleTask != null && !rumbleTask.IsCompleted)
                return;

            // Create a new task that executes the following code
            rumbleTask = Task.Run(async () =>
            {
                SetVibration(LargeMotor, SmallMotor);
                await Task.Delay(delay);
                SetVibration(0, 0);
            });
        }

        // this function cannot be called twice
        public virtual void Plug()
        {
            SetVibrationStrength(SettingsManager.GetUInt("VibrationStrength"));

            InjectedButtons.Clear();

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                ui_button_hook.IsEnabled = false;
            });
        }

        // this function cannot be called twice
        public virtual void Unplug()
        {
            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                ui_button_hook.IsEnabled = true;
            });
        }

        public bool IsHidden()
        {
            // bool hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            bool hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
            return /* hide_device || */ hide_base;
        }

        public virtual void Hide(bool powerCycle = true)
        {
            HideHID();

            if (powerCycle)
            {
                IsBusy = true;

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;
                CyclePort();
            }

            UpdateUI();
        }

        public virtual void Unhide(bool powerCycle = true)
        {
            UnhideHID();

            if (powerCycle)
            {
                IsBusy = true;

                ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;
                CyclePort();
            }

            UpdateUI();
        }

        public virtual void CyclePort()
        {
            Details.CyclePort();
        }

        public virtual void SetLightColor(byte R, byte G, byte B)
        {
        }

        protected void HideHID()
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

        protected void UnhideHID()
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

        public virtual async void Calibrate()
        {
            SensorsManager.Calibrate(gamepadMotions);
        }

        protected virtual void ui_button_calibrate_Click(object sender, RoutedEventArgs e)
        {
            Calibrate();
        }

        protected virtual void ui_button_hide_Click(object sender, RoutedEventArgs e)
        { }

        protected virtual void ui_button_hook_Click(object sender, RoutedEventArgs e)
        { }

        public virtual string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.DPadUp:
                    return "\u219F";
                case ButtonFlags.DPadDown:
                    return "\u21A1";
                case ButtonFlags.DPadLeft:
                    return "\u219E";
                case ButtonFlags.DPadRight:
                    return "\u21A0";

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

                case ButtonFlags.VolumeUp:
                    return "\u21fe";
                case ButtonFlags.VolumeDown:
                    return "\u21fd";

                case ButtonFlags.LeftPadTouch:
                    return "\u2268";
                case ButtonFlags.RightPadTouch:
                    return "\u2269";

                case ButtonFlags.LeftPadClick:
                    return "\u2266";
                case ButtonFlags.RightPadClick:
                    return "\u2267";

                case ButtonFlags.LeftPadClickDown:
                    return "\u2274";
                case ButtonFlags.LeftPadClickUp:
                    return "\u2270";
                case ButtonFlags.LeftPadClickRight:
                    return "\u2272";
                case ButtonFlags.LeftPadClickLeft:
                    return "\u226E";

                case ButtonFlags.RightPadClickDown:
                    return "\u2275";
                case ButtonFlags.RightPadClickUp:
                    return "\u2271";
                case ButtonFlags.RightPadClickRight:
                    return "\u2273";
                case ButtonFlags.RightPadClickLeft:
                    return "\u226F";

                case ButtonFlags.L4:
                    return "\u2276";
                case ButtonFlags.L5:
                    return "\u2278";
                case ButtonFlags.R4:
                    return "\u2277";
                case ButtonFlags.R5:
                    return "\u2279";
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

                case AxisFlags.LeftPadX:
                    return "\u226A";
                case AxisFlags.LeftPadY:
                    return "\u226B";
                case AxisFlags.RightPadX:
                    return "\u226C";
                case AxisFlags.RightPadY:
                    return "\u226D";
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

                case AxisLayoutFlags.LeftPad:
                    return "\u2264";
                case AxisLayoutFlags.RightPad:
                    return "\u2265";
            }

            return defaultGlyph;
        }

        public GlyphIconInfo GetGlyphIconInfo(ButtonFlags button, int fontIconSize = 14)
        {
            string? glyph = GetGlyph(button);
            return new GlyphIconInfo
            {
                Name = GetButtonName(button),
                Glyph = glyph is not null ? glyph : defaultGlyph,
                FontSize = fontIconSize,
                FontFamily = GlyphFontFamily,
                Foreground = GetGlyphColor(button)
            };
        }

        public GlyphIconInfo GetGlyphIconInfo(AxisLayoutFlags axis, int fontIconSize = 14)
        {
            string? glyph = GetGlyph(axis);
            return new GlyphIconInfo
            {
                Name = GetAxisName(axis),
                Glyph = glyph is not null ? glyph : defaultGlyph,
                FontSize = fontIconSize,
                FontFamily = GlyphFontFamily,
                Foreground = GetGlyphColor(axis)
            };
        }

        [Obsolete("GetFontIcon has dependencies on UI and should be avoided. Use GetGlyphIconInfo instead.")]
        public FontIcon GetFontIcon(ButtonFlags button, int FontIconSize = 14)
        {
            var FontIcon = new FontIcon
            {
                Glyph = GetGlyph(button),
                FontSize = FontIconSize,
                Foreground = GetGlyphColor(button)
            };

            if (FontIcon.Glyph is not null)
                FontIcon.FontFamily = GlyphFontFamily;

            return FontIcon;
        }


        [Obsolete("GetFontIcon has dependencies on UI and should be avoided. Use GetGlyphIconInfo instead.")]
        public FontIcon GetFontIcon(AxisLayoutFlags axis, int FontIconSize = 14)
        {
            var FontIcon = new FontIcon
            {
                Glyph = GetGlyph(axis),
                FontSize = FontIconSize,
                Foreground = GetGlyphColor(axis)
            };

            if (FontIcon.Glyph is not null)
                FontIcon.FontFamily = GlyphFontFamily;

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

        public List<ButtonFlags> GetTargetButtons()
        {
            IEnumerable<ButtonFlags> buttons = Enum.GetValues(typeof(ButtonFlags)).Cast<ButtonFlags>();

            return buttons.Where(a => TargetButtons.Contains(a)).ToList();
        }

        public List<AxisLayoutFlags> GetTargetAxis()
        {
            IEnumerable<AxisLayoutFlags> axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => TargetAxis.Contains(a) && !IsTrigger(a)).ToList();
        }

        public List<AxisLayoutFlags> GetTargetTriggers()
        {
            IEnumerable<AxisLayoutFlags> axis = Enum.GetValues(typeof(AxisLayoutFlags)).Cast<AxisLayoutFlags>();

            return axis.Where(a => TargetAxis.Contains(a) && IsTrigger(a)).ToList();
        }

        public bool HasSourceButton(ButtonFlags button)
        {
            return SourceButtons.Contains(button);
        }

        public bool HasSourceButton(List<ButtonFlags> buttons)
        {
            return SourceButtons.Any(buttons.Contains);
        }

        public bool HasSourceAxis(AxisLayoutFlags axis)
        {
            return SourceAxis.Contains(axis);
        }

        public bool HasSourceAxis(List<AxisLayoutFlags> axis)
        {
            return SourceAxis.Any(axis.Contains);
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
        public event UserIndexChangedEventHandler UserIndexChanged;
        public delegate void UserIndexChangedEventHandler(byte UserIndex);

        public event InputsUpdatedEventHandler InputsUpdated;
        public delegate void InputsUpdatedEventHandler(ControllerState Inputs, Dictionary<byte, GamepadMotion> gamepadMotions, float delta, byte gamepadIndex);
        #endregion
    }
}
