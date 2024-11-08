using HandheldCompanion.Actions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HandheldCompanion.Controllers
{
    public class IController
    {
        #region events
        public event UserIndexChangedEventHandler UserIndexChanged;
        public delegate void UserIndexChangedEventHandler(byte UserIndex);

        public event StateChangedEventHandler StateChanged;
        public delegate void StateChangedEventHandler();

        public event InputsUpdatedEventHandler InputsUpdated;
        public delegate void InputsUpdatedEventHandler(ControllerState Inputs, Dictionary<byte, GamepadMotion> gamepadMotions, float delta, byte gamepadIndex);
        #endregion

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

        protected static readonly FontFamily GlyphFontFamily = new("PromptFont");
        protected static readonly string defaultGlyph = "\u2753";

        public ControllerCapabilities Capabilities = ControllerCapabilities.None;
        protected SortedDictionary<AxisLayoutFlags, Color> ColoredAxis = [];
        protected SortedDictionary<ButtonFlags, Color> ColoredButtons = [];

        public PnPDetails Details;

        public ButtonState InjectedButtons = new();
        public ControllerState Inputs = new();

        protected byte gamepadIndex = 0;
        protected Dictionary<byte, GamepadMotion> gamepadMotions = new();

        protected double VibrationStrength = 1.0d;
        private Task rumbleTask;

        protected object hidLock = new();

        public virtual bool IsReady => true;

        public virtual bool IsWireless => Details.isBluetooth;
        public virtual bool IsDongle => Details.isDongle;

        public bool isPlaceholder;

        private bool _IsBusy;
        public bool IsBusy
        {
            get
            {
                return _IsBusy;
            }

            set
            {
                if (value == _IsBusy)
                    return;

                _IsBusy = value;
                StateChanged?.Invoke();
            }
        }

        private byte _UserIndex = 255;
        protected byte UserIndex
        {
            get
            {
                return _UserIndex;
            }

            set
            {
                if (value == _UserIndex)
                    return;

                _UserIndex = value;
                UserIndexChanged?.Invoke(value);
            }
        }

        public IController()
        {
            gamepadMotions[gamepadIndex] = new(string.Empty, CalibrationMode.Manual);
            InitializeInputOutput();
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
            gamepadMotions[gamepadIndex] = new(details.deviceInstanceId, CalibrationMode.Manual | CalibrationMode.SensorFusion);
        }

        public virtual void UpdateInputs(long ticks, float delta)
        {
            if (IsBusy)
                return;

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
            if (isPlaceholder)
                return;

            SetVibrationStrength(SettingsManager.GetUInt("VibrationStrength"));

            InjectedButtons.Clear();
        }

        // this function cannot be called twice
        public virtual void Unplug()
        {
            if (isPlaceholder)
                return;
        }

        public bool IsHidden()
        {
            // bool hide_device = HidHide.IsRegistered(Details.deviceInstanceId);
            bool hide_base = HidHide.IsRegistered(Details.baseContainerDeviceInstanceId);
            return /* hide_device || */ hide_base;
        }

        public virtual void Hide(bool powerCycle = true)
        {
            if (isPlaceholder)
                return;

            HideHID();

            if (powerCycle)
                CyclePort();
        }

        public virtual void Unhide(bool powerCycle = true)
        {
            if (isPlaceholder)
                return;

            UnhideHID();

            if (powerCycle)
                CyclePort();
        }

        public virtual void CyclePort()
        {
            // set status
            IsBusy = true;
            ControllerManager.PowerCyclers[Details.baseContainerDeviceInstanceId] = true;

            Details.CyclePort();
        }

        public virtual void SetLightColor(byte R, byte G, byte B)
        {
        }

        protected void HideHID()
        {
            HidHide.HidePath(Details.baseContainerDeviceInstanceId);
            HidHide.HidePath(Details.deviceInstanceId);
        }

        protected void UnhideHID()
        {
            HidHide.UnhidePath(Details.baseContainerDeviceInstanceId);
            HidHide.UnhidePath(Details.deviceInstanceId);
        }

        public virtual bool RestoreDrivers()
        {
            return true;
        }

        public virtual async void Calibrate()
        {
            SensorsManager.Calibrate(gamepadMotions);
        }

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
                Color = GetGlyphColor(button)
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
                Color = GetGlyphColor(axis)
            };
        }

        public Color GetGlyphColor(ButtonFlags button)
        {
            if (ColoredButtons.TryGetValue(button, out Color color))
                return color;

            return Colors.White;
        }

        public Color GetGlyphColor(AxisLayoutFlags axis)
        {
            /* if (AxisBrush.TryGetValue(axis, out Brush brush))
                return brush; */

            return Colors.White;
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

        public override string ToString()
        {
            if (Details is not null)
                return Details.Name;
            return string.Empty;
        }
    }
}
