using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Dummies;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    // ViewModel used to fill Target ComboBox on Mappings
    public class MappingTargetViewModel : BaseViewModel
    {
        public object? Tag { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsSupported { get; set; } = true;

        public override string ToString()
        {
            return Content;
        }
    }

    public abstract class MappingViewModel : BaseViewModel
    {
        #region Core action properties

        protected object Value { get; set; }
        public IActions? Action { get; protected set; }

        public int ActionTypeIndex
        {
            get => Action is not null ? (int)Action.actionType : 0;
            set
            {
                if (value != ActionTypeIndex)
                {
                    ActionTypeChanged((ActionType)value);
                    OnPropertyChanged(nameof(ActionTypeIndex));
                    OnPropertyChanged(nameof(SupportedActionTypes));
                }
            }
        }

        public virtual ActionType[] SupportedActionTypes => [ActionType.Disabled];

        public string ActionTypeName => ((ActionType)ActionTypeIndex).ToString();

        public string ActionTypeDisplayName => GetActionTypeDisplayName();

        public string TargetDisplayName => SelectedTarget?.Content ?? string.Empty;

        public string TurboDisplayName => GetStateDisplayName(HasTurbo);

        public string ToggleDisplayName => GetStateDisplayName(HasToggle);

        public string InputShiftDisplayName => GetInputShiftDisplayName();

        public bool HasTurbo
        {
            get => Action is not null && Action.HasTurbo;
            set
            {
                if (Action is not null && value != HasTurbo)
                {
                    Action.HasTurbo = value;
                    OnPropertyChanged(nameof(HasTurbo));
                }
            }
        }

        public bool HasInterruptable
        {
            get => Action is not null && Action.HasInterruptable;
            set
            {
                if (Action is not null && value != HasInterruptable)
                {
                    Action.HasInterruptable = value;
                    OnPropertyChanged(nameof(HasInterruptable));
                }
            }
        }

        public bool HasToggle
        {
            get => Action is not null && Action.HasToggle;
            set
            {
                if (Action is not null && value != HasToggle)
                {
                    Action.HasToggle = value;
                    OnPropertyChanged(nameof(HasToggle));
                }
            }
        }

        public float TurboDelay
        {
            get => Action is not null ? Action.TurboDelay : 0;
            set
            {
                if (Action is not null && value != TurboDelay)
                {
                    Action.TurboDelay = value;
                    OnPropertyChanged(nameof(TurboDelay));
                }
            }
        }

        public float StartDelay
        {
            get => Action is not null ? Action.StartDelay : 0;
            set
            {
                double rounded = Math.Round(value);
                if (Action is not null && rounded != StartDelay)
                {
                    Action.StartDelay = (float)rounded;
                    OnPropertyChanged(nameof(StartDelay));
                }
            }
        }

        public ObservableCollection<MappingTargetViewModel> Targets { get; set; } = [];

        public bool HasTarget => SelectedTarget is not null;

        private MappingTargetViewModel? _selectedTarget;
        public MappingTargetViewModel? SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (value != SelectedTarget)
                {
                    _selectedTarget = value;
                    IsSupported = value?.IsSupported ?? true;
                    TargetTypeChanged();
                    OnPropertyChanged(nameof(SelectedTarget));
                    OnPropertyChanged(nameof(HasTarget));
                }
            }
        }

        public int Axis2ButtonThreshold
        {
            get => (int)((Action is IActions iActions) ? iActions.motionThreshold : 0);
            set
            {
                if (Action is IActions iActions && value != Axis2ButtonThreshold)
                {
                    iActions.motionThreshold = value;
                    OnPropertyChanged(nameof(Axis2ButtonThreshold));
                }
            }
        }

        private bool _isSupported = true;
        public bool IsSupported
        {
            get => _isSupported;
            set
            {
                if (value != _isSupported)
                {
                    _isSupported = value;
                    OnPropertyChanged(nameof(IsSupported));
                }
            }
        }

        #endregion

        #region Mouse mapping properties

        // Shows the Axis2 panel for Mouse actions of type Scroll or Move
        public Visibility Axis2MouseVisibility
        {
            get
            {
                if (SelectedTarget == null)
                    return Visibility.Collapsed;

                // Check if the current action is a Mouse action
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType != ActionType.Mouse)
                    return Visibility.Collapsed;

                // Only show if the mouse action is Scroll or Move
                if (SelectedTarget.Tag is not MouseActionsType mouseAction)
                    return Visibility.Collapsed;

                return (mouseAction == MouseActionsType.Scroll || mouseAction == MouseActionsType.Move)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // Shows the Axis2 panel for Button, Keyboard,
        // or for Mouse actions that are NOT Scroll or Move.
        public Visibility Axis2ButtonVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Button || currentActionType == ActionType.Keyboard)
                    return Visibility.Visible;

                if (currentActionType == ActionType.Touchpad &&
                    Action is TouchpadActions { TargetType: TouchpadTargetType.Button })
                    return Visibility.Visible;

                // For Mouse actions, ensure a target exists and check its action type
                if (currentActionType == ActionType.Mouse && SelectedTarget != null)
                {
                    if (SelectedTarget.Tag is not MouseActionsType mouseAction)
                        return Visibility.Collapsed;

                    if (mouseAction != MouseActionsType.Scroll && mouseAction != MouseActionsType.Move)
                        return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        // Base properties for Mouse position (MoveTo) - return Collapsed by default
        // Overridden in ButtonMappingViewModel and AxisMappingViewModel
        public virtual Visibility Button2MouseTo => Visibility.Collapsed;
        public virtual Visibility Axis2MouseTo => Visibility.Collapsed;

        public virtual double Button2MouseToX
        {
            get => 0;
            set { }
        }

        public virtual double Button2MouseToY
        {
            get => 0;
            set { }
        }

        public virtual double Axis2MouseToX
        {
            get => 0;
            set { }
        }

        public virtual double Axis2MouseToY
        {
            get => 0;
            set { }
        }

        public virtual bool Button2MouseRestore
        {
            get => false;
            set { }
        }

        public virtual bool Axis2MouseRestore
        {
            get => false;
            set { }
        }

        // Axis to Mouse properties (for Mouse actions) - default to 0/false
        public virtual int Axis2MousePointerSpeed { get => 0; set { } }
        public virtual float Axis2MouseAcceleration { get => 0; set { } }
        public virtual int Axis2MouseDeadzone { get => 0; set { } }
        public virtual bool Axis2MouseFiltering { get => false; set { } }
        public virtual float Axis2MouseFilterCutoff { get => 0; set { } }

        #endregion

        #region Axis mapping properties

        // Axis direction properties (for Axis2Button) - default to false
        // These should only be visible for AxisMappingViewModel, not ButtonMappingViewModel
        public virtual bool IsUp { get => false; set { } }
        public virtual bool IsDown { get => false; set { } }
        public virtual bool IsLeft { get => false; set { } }
        public virtual bool IsRight { get => false; set { } }

        // Property to check if this is an Axis mapping (for visibility conditions)
        public virtual bool IsAxisMapping => false;

        // Axis to Axis properties (for Joystick actions) - default to 0/false
        // Should only be visible for Axis -> Joystick mappings
        public virtual int Axis2AxisOutputShapeIndex { get => 0; set { } }
        public virtual bool Axis2AxisInvertHorizontal { get => false; set { } }
        public virtual bool Axis2AxisInvertVertical { get => false; set { } }
        public virtual int Axis2AxisInnerDeadzone { get => 0; set { } }
        public virtual int Axis2AxisOuterDeadzone { get => 0; set { } }
        public virtual int Axis2AxisAntiDeadzone { get => 0; set { } }
        public virtual int Button2AxisX { get => 0; set { } }
        public virtual int Button2AxisY { get => 0; set { } }
        public virtual Visibility AxisResponseCurveVisibility => Visibility.Collapsed;
        public virtual Visibility AxisOutputShapeVisibility =>
            ActionTypeIndex == (int)ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;

        // Visibility for Axis invert properties - only Axis mappings
        public virtual Visibility AxisInvertVisibility => Visibility.Collapsed;
        public virtual Visibility AxisDeadzoneVisibility => Visibility.Collapsed;
        public virtual Visibility Button2AxisVisibility => Visibility.Collapsed;
        public virtual Visibility AxisVisualizerVisibility => Visibility.Collapsed;
        public virtual double AxisVisualizerDotX => 0.0d;
        public virtual double AxisVisualizerDotY => 0.0d;
        public virtual double AxisVisualizerDotTranslateX => 0.0d;
        public virtual double AxisVisualizerDotTranslateY => 0.0d;
        public virtual double AxisVisualizerInnerDeadzoneSize => 0.0d;
        public virtual double AxisVisualizerOuterDeadzoneSize => 0.0d;
        public virtual double AxisVisualizerAntiDeadzoneSize => 0.0d;

        // Axis visibility properties - default to Collapsed
        public virtual Visibility Axis2TouchpadVisibility => Visibility.Collapsed;
        public virtual Visibility Axis2JoystickVisibility => Visibility.Collapsed;

        // Axis Direction and Threshold should only be visible for Axis mappings converting to Button
        public virtual Visibility AxisDirectionVisibility => Visibility.Collapsed;
        public virtual Visibility AxisThresholdVisibility => Visibility.Collapsed;

        #endregion

        #region Trigger mapping properties

        // Trigger to Trigger/Axis deadzone properties - default to 0
        // Should only be visible for Trigger -> Trigger/Axis mappings
        public virtual int Trigger2TriggerInnerDeadzone { get => 0; set { } }
        public virtual int Trigger2TriggerOuterDeadzone { get => 0; set { } }
        public virtual int Trigger2TriggerAntiDeadzone { get => 0; set { } }

        // Visibility for Trigger deadzone properties - only Trigger -> Trigger/Axis
        public virtual Visibility TriggerDeadzoneVisibility => Visibility.Collapsed;

        // Property to check if this is a Trigger mapping (for visibility conditions)
        public virtual bool IsTriggerMapping => false;

        // Trigger output (for Trigger actions) - default to 0
        // Should only be visible for Button -> Trigger mappings
        public virtual float TriggerOutput
        {
            get => 0;
            set { }
        }

        // Visibility for Trigger output - only Button -> Trigger
        public virtual Visibility TriggerOutputVisibility => Visibility.Collapsed;
        public virtual Visibility Trigger2ButtonVisibility => Visibility.Collapsed;

        #endregion

        #region Touchpad mapping properties

        public Visibility TouchpadActionTypeVisibility
        {
            get
            {
                IController controller = ControllerManager.GetDefault(true);
                return Action is TouchpadActions || TouchpadActions.HasTargets(controller)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public Visibility TouchpadAxisActionTypeVisibility
        {
            get
            {
                IController controller = ControllerManager.GetDefault(true);
                return Action is TouchpadActions || TouchpadActions.GetAxisTargets(controller).Any()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        public virtual Visibility TouchpadSettingsVisibility =>
            ActionTypeIndex == (int)ActionType.Touchpad &&
            Action is TouchpadActions { TargetType: TouchpadTargetType.Button } touchpadAction &&
            TouchpadActions.IsGestureTarget(touchpadAction.Button)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public virtual Visibility TouchpadSwipeSettingsVisibility =>
            ActionTypeIndex == (int)ActionType.Touchpad &&
            Action is TouchpadActions { TargetType: TouchpadTargetType.Button, Button: ButtonFlags.TouchpadSwipe }
                ? Visibility.Visible
                : Visibility.Collapsed;
        public virtual Visibility TouchpadCoordinateSettingsVisibility =>
            ActionTypeIndex == (int)ActionType.Touchpad &&
            Action is TouchpadActions { TargetType: TouchpadTargetType.Button, Button: ButtonFlags.TouchpadClick or ButtonFlags.TouchpadTouch }
                ? Visibility.Visible
                : Visibility.Collapsed;
        public virtual bool TouchpadUseCoordinates
        {
            get => Action is TouchpadActions a && a.UseCoordinates;
            set
            {
                if (Action is TouchpadActions a && value != a.UseCoordinates)
                {
                    a.UseCoordinates = value;
                    OnPropertyChanged(nameof(TouchpadUseCoordinates));
                    OnPropertyChanged(nameof(TouchpadVisualizerVisibility));
                }
            }
        }
        public virtual Visibility TouchpadVisualizerVisibility => TouchpadSettingsVisibility;
        public double TouchpadVisualizerStartX => TouchpadVisualizerPosition(TouchpadX, TouchpadMaxX, 232.0d);
        public double TouchpadVisualizerStartY => TouchpadVisualizerPosition(TouchpadY, TouchpadMaxY, 112.0d);
        public double TouchpadVisualizerEndX => TouchpadVisualizerPosition(TouchpadEndX, TouchpadMaxX, 232.0d);
        public double TouchpadVisualizerEndY => TouchpadVisualizerPosition(TouchpadEndY, TouchpadMaxY, 112.0d);
        public double TouchpadVisualizerLineStartX => TouchpadVisualizerStartX + 4.0d;
        public double TouchpadVisualizerLineStartY => TouchpadVisualizerStartY + 4.0d;
        public double TouchpadVisualizerLineEndX => TouchpadVisualizerEndX + 4.0d;
        public double TouchpadVisualizerLineEndY => TouchpadVisualizerEndY + 4.0d;
        public int TouchpadMaxX => DS4Touch.TOUCHPAD_WIDTH - 1;
        public int TouchpadMaxY => DS4Touch.TOUCHPAD_HEIGHT - 1;
        public double TouchpadMaxDuration => TouchpadActions.MaximumSwipeDuration;
        public int TouchpadFingerIndex
        {
            get => Action is TouchpadActions a ? a.Finger - 1 : 0;
            set
            {
                if (Action is TouchpadActions a && value is >= 0 and <= 1)
                {
                    a.Finger = (byte)(value + 1);
                    OnPropertyChanged(nameof(TouchpadFingerIndex));
                }
            }
        }
        public string TouchpadCoordinateRangeDescription => string.Format(
            Resources.LayoutPage_TouchpadCoordinateRange, TouchpadMaxX, TouchpadMaxY);
        public int TouchpadX { get => Action is TouchpadActions a ? a.X : 0; set { if (Action is TouchpadActions a) { a.X = value; OnPropertyChanged(nameof(TouchpadX)); NotifyTouchpadVisualizer(nameof(TouchpadVisualizerStartX), nameof(TouchpadVisualizerStartY), nameof(TouchpadVisualizerLineStartX), nameof(TouchpadVisualizerLineStartY)); } } }
        public int TouchpadY { get => Action is TouchpadActions a ? a.Y : 0; set { if (Action is TouchpadActions a) { a.Y = value; OnPropertyChanged(nameof(TouchpadY)); NotifyTouchpadVisualizer(nameof(TouchpadVisualizerStartX), nameof(TouchpadVisualizerStartY), nameof(TouchpadVisualizerLineStartX), nameof(TouchpadVisualizerLineStartY)); } } }
        public int TouchpadEndX { get => Action is TouchpadActions a ? a.EndX : 0; set { if (Action is TouchpadActions a) { a.EndX = value; OnPropertyChanged(nameof(TouchpadEndX)); NotifyTouchpadVisualizer(nameof(TouchpadVisualizerEndX), nameof(TouchpadVisualizerLineEndX)); } } }
        public int TouchpadEndY { get => Action is TouchpadActions a ? a.EndY : 0; set { if (Action is TouchpadActions a) { a.EndY = value; OnPropertyChanged(nameof(TouchpadEndY)); NotifyTouchpadVisualizer(nameof(TouchpadVisualizerEndY), nameof(TouchpadVisualizerLineEndY)); } } }
        public double TouchpadSwipeDuration { get => Action is TouchpadActions a ? a.SwipeDuration : TouchpadActions.DefaultSwipeDuration; set { if (Action is TouchpadActions a) { a.SwipeDuration = (float)value; OnPropertyChanged(nameof(TouchpadSwipeDuration)); } } }

        private static double TouchpadVisualizerPosition(int value, int maximum, double available) =>
            maximum == 0 ? 0.0d : value * available / maximum;

        private void NotifyTouchpadVisualizer(params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
                OnPropertyChanged(propertyName);
        }

        protected static IEnumerable<ButtonFlags> GetButtonTargets(IController controller)
        {
            foreach (ButtonFlags button in controller.GetTargetButtons())
            {
                if (!TouchpadActions.IsTouchpadButton(button))
                    yield return button;
            }
        }

        protected static IEnumerable<AxisLayoutFlags> GetJoystickTargets(IController controller) =>
            controller.GetTargetAxis().Where(axis => !TouchpadActions.IsTouchpadAxis(axis));

        protected void SetTouchpadTarget(object target)
        {
            if (Action is not TouchpadActions touchpadAction)
                return;

            if (target is ButtonFlags button)
                touchpadAction.SetTarget(button);
            else if (target is AxisLayoutFlags axis)
                touchpadAction.SetTarget(axis);
        }

        #endregion

        #region Section visibility

        // Combined visibility for settings that apply to both Button mappings and Axis2Button mappings
        // This avoids duplication - shows when ActionTypeIndex is Button/Keyboard/Mouse/Trigger/Shift
        // OR when it's an Axis mapping converting to Button
        public Visibility GeneralActionVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                // Show for Button, Keyboard, Mouse, Trigger, Shift
                if (currentActionType == ActionType.Button || currentActionType == ActionType.Keyboard ||
                    currentActionType == ActionType.Mouse || currentActionType == ActionType.Trigger ||
                    currentActionType == ActionType.Shift || currentActionType == ActionType.Joystick ||
                    currentActionType == ActionType.Touchpad)
                    return Visibility.Visible;

                // For Axis mappings converting to Button, also show
                if (IsAxisMapping && Axis2ButtonVisibility == Visibility.Visible)
                    return Visibility.Visible;

                if (IsAxisMapping && AxisResponseCurveVisibility == Visibility.Visible)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        // Container visibility properties - check if section should show based on children visibility
        // Mouse settings section: only visible if at least one mouse-related setting is visible
        public virtual Visibility MouseSettingsSectionVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType != ActionType.Mouse)
                    return Visibility.Collapsed;

                // Check if any mouse-related child is visible
                if (Button2MouseTo == Visibility.Visible ||
                    Axis2MouseTo == Visibility.Visible ||
                    Axis2MouseVisibility == Visibility.Visible ||
                    Axis2TouchpadVisibility == Visibility.Visible ||
                    Axis2MouseFiltering ||
                    Axis2JoystickVisibility == Visibility.Visible)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        // Axis settings section: only visible if at least one axis-related setting is visible
        // For Axis->Joystick mappings, show if any axis property is visible
        public virtual Visibility AxisSettingsSectionVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Touchpad || currentActionType == ActionType.Button || currentActionType == ActionType.Keyboard || currentActionType == ActionType.Mouse)
                    return Visibility.Visible;

                if (currentActionType != ActionType.Joystick)
                    return Visibility.Collapsed;

                // For Button->Joystick, always show the section (it has Output shape which is always visible)
                if (!IsAxisMapping)
                    return Visibility.Visible;

                // For Axis->Joystick, check if at least one setting is visible
                if (AxisVisualizerVisibility == Visibility.Visible ||
                    AxisInvertVisibility == Visibility.Visible ||
                    Axis2MouseVisibility == Visibility.Visible)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        // Trigger settings section: only visible if at least one trigger setting is visible
        public virtual Visibility TriggerSettingsSectionVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType != ActionType.Trigger)
                    return Visibility.Collapsed;

                // Check if any trigger-related child is visible
                if (TriggerOutputVisibility == Visibility.Visible ||
                    TriggerDeadzoneVisibility == Visibility.Visible)
                    return Visibility.Visible;

                return Visibility.Collapsed;
            }
        }

        // Timing settings section: only visible if at least one timing setting is visible
        public virtual Visibility TimingSettingsSectionVisibility
        {
            get
            {
                // Timing settings are only available for actions in GeneralActionVisibility
                if (GeneralActionVisibility == Visibility.Collapsed)
                    return Visibility.Collapsed;

                // Check if any timing-related child would be visible
                // This includes: HasToggle, HasTurbo, HasDuration, and StartDelay properties
                // All of these require GeneralActionVisibility to be visible
                return Visibility.Visible;
            }
        }

        #endregion

        #region Modifier and press properties

        // Modifier properties (for Keyboard/Mouse actions) - default to 0/false
        public virtual int ModifierIndex { get => 0; set { } }
        public virtual bool HasModifier => false;

        // Press type properties (for Button actions) - default to 0/false
        public virtual int PressTypeIndex { get => 0; set { } }
        public virtual string PressTypeTooltip => string.Empty;
        public virtual bool HasDuration => false;
        public virtual float LongPressDelay { get => 0; set { } }

        #endregion

        #region Haptic properties

        // Haptic properties - default to 0
        public bool UsesMovementHaptics =>
            IsAxisMapping &&
            (Action is TouchpadActions { TargetType: TouchpadTargetType.Axis } ||
             (Value is AxisLayoutFlags sourceAxis &&
              TouchpadActions.IsTouchpadAxis(sourceAxis) &&
              Action is MouseActions { MouseType: MouseActionsType.Move or MouseActionsType.Scroll }));

        public bool MovementHapticsEnabled
        {
            get => Action?.HapticMode is HapticMode.Down or HapticMode.Both;
            set
            {
                if (Action is null || value == MovementHapticsEnabled)
                    return;

                Action.HapticMode = value ? HapticMode.Down : HapticMode.Off;
                OnPropertyChanged(nameof(MovementHapticsEnabled));
                OnPropertyChanged(nameof(HapticModeIndex));
            }
        }

        public virtual int HapticModeIndex
        {
            get => Action is not null ? (int)Action.HapticMode : 0;
            set
            {
                if (Action is not null && value != HapticModeIndex)
                {
                    Action.HapticMode = (HapticMode)value;
                    OnPropertyChanged(nameof(HapticModeIndex));
                    OnPropertyChanged(nameof(MovementHapticsEnabled));
                }
            }
        }

        public virtual int HapticStrengthIndex
        {
            get => Action is not null ? (int)Action.HapticStrength : 0;
            set
            {
                if (Action is not null && value != HapticStrengthIndex)
                {
                    Action.HapticStrength = (HapticStrength)value;
                    OnPropertyChanged(nameof(HapticStrengthIndex));
                }
            }
        }

        #endregion

        #region Shift properties

        // Shift properties - default to Always enabled (index 1)
        public virtual int ShiftModeIndex
        {
            get
            {
                if (Action is null) return 1; // Default to always enabled
                if ((Action.ShiftSlot & ShiftSlot.Any) != 0) return 1; // Always enabled
                if (Action.ShiftSlot == ShiftSlot.None) return 0; // Disabled on shift

                // Check if it's OR mode or strict mode
                if (Action.ShiftMatchAny) return 3; // Enabled on shift (any)
                return 2; // Enabled on shift (strict)
            }
            set
            {
                if (Action is null || value == ShiftModeIndex) return;

                switch (value)
                {
                    case 0: // Disabled on shift
                        Action.ShiftSlot = ShiftSlot.None;
                        Action.ShiftMatchAny = false;
                        break;
                    case 1: // Always enabled
                        Action.ShiftSlot = ShiftSlot.Any;
                        Action.ShiftMatchAny = false;
                        break;
                    case 2: // Enabled on shift (strict)
                        if (Action.ShiftSlot == ShiftSlot.None || Action.ShiftSlot == ShiftSlot.Any)
                            Action.ShiftSlot = ShiftSlot.ShiftA;
                        Action.ShiftMatchAny = false;
                        break;
                    case 3: // Enabled on shift (any/OR)
                        if (Action.ShiftSlot == ShiftSlot.None || Action.ShiftSlot == ShiftSlot.Any)
                            Action.ShiftSlot = ShiftSlot.ShiftA;
                        Action.ShiftMatchAny = true;
                        break;
                }
                OnPropertyChanged(nameof(ShiftModeIndex));
                OnPropertyChanged(nameof(ShowShiftSelection));
                OnPropertyChanged(nameof(ShiftA));
                OnPropertyChanged(nameof(ShiftB));
                OnPropertyChanged(nameof(ShiftC));
                OnPropertyChanged(nameof(ShiftD));
            }
        }

        public virtual bool ShowShiftSelection => ShiftModeIndex == 2 || ShiftModeIndex == 3;

        public virtual bool ShiftA
        {
            get => Action is not null && (Action.ShiftSlot & ShiftSlot.ShiftA) != 0;
            set
            {
                if (Action is null || value == ShiftA) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftA
                    : Action.ShiftSlot & ~ShiftSlot.ShiftA;
                OnPropertyChanged(nameof(ShiftA));
            }
        }

        public virtual bool ShiftB
        {
            get => Action is not null && (Action.ShiftSlot & ShiftSlot.ShiftB) != 0;
            set
            {
                if (Action is null || value == ShiftB) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftB
                    : Action.ShiftSlot & ~ShiftSlot.ShiftB;
                OnPropertyChanged(nameof(ShiftB));
            }
        }

        public virtual bool ShiftC
        {
            get => Action is not null && (Action.ShiftSlot & ShiftSlot.ShiftC) != 0;
            set
            {
                if (Action is null || value == ShiftC) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftC
                    : Action.ShiftSlot & ~ShiftSlot.ShiftC;
                OnPropertyChanged(nameof(ShiftC));
            }
        }

        public virtual bool ShiftD
        {
            get => Action is not null && (Action.ShiftSlot & ShiftSlot.ShiftD) != 0;
            set
            {
                if (Action is null || value == ShiftD) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftD
                    : Action.ShiftSlot & ~ShiftSlot.ShiftD;
                OnPropertyChanged(nameof(ShiftD));
            }
        }

        #endregion

        #region Display helpers

        protected string GetActionTypeDisplayName()
        {
            ActionType actionType = (ActionType)ActionTypeIndex;
            string resourceKey = $"LayoutPage_ActionType_{actionType}";
            return Resources.ResourceManager.GetString(resourceKey) ?? actionType.ToString();
        }

        protected static string GetStateDisplayName(bool isEnabled)
        {
            return isEnabled ? "Enabled" : "Disabled";
        }

        protected string GetInputShiftDisplayName()
        {
            if (Action is null)
                return EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.Any);

            return ShiftModeIndex switch
            {
                0 => EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.None),
                1 => EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.Any),
                2 => $"Shift: {GetSelectedShiftSlotsDisplayName()} (Strict)",
                3 => $"Shift: {GetSelectedShiftSlotsDisplayName()} (Any)",
                _ => EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.Any),
            };
        }

        protected string GetSelectedShiftSlotsDisplayName()
        {
            List<string> selectedShiftSlots = new List<string>();

            if (ShiftA)
                selectedShiftSlots.Add(EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.ShiftA));

            if (ShiftB)
                selectedShiftSlots.Add(EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.ShiftB));

            if (ShiftC)
                selectedShiftSlots.Add(EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.ShiftC));

            if (ShiftD)
                selectedShiftSlots.Add(EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.ShiftD));

            return selectedShiftSlots.Count != 0
                ? string.Join(", ", selectedShiftSlots)
                : EnumUtils.GetDescriptionFromEnumValue(ShiftSlot.None);
        }

        #endregion

        #region Notifications and lifecycle

        // Purely UI related properties, they should NOT update the Layout
        // Avoid unnecessary save/update calls
        protected HashSet<string> ExcludedUpdateProperties =
        [
            nameof(IsSupported),
            nameof(ActionTypeDisplayName),
            nameof(TargetDisplayName),
            nameof(TurboDisplayName),
            nameof(ToggleDisplayName),
            nameof(InputShiftDisplayName),
            nameof(TouchpadActionTypeVisibility),
            nameof(TouchpadAxisActionTypeVisibility),
            nameof(TouchpadCoordinateSettingsVisibility),
            nameof(UsesMovementHaptics),
            nameof(MovementHapticsEnabled),
        ];

        public override void OnPropertyChanged(string? propertyName)
        {
            switch (propertyName)
            {
                case "":
                case nameof(SelectedTarget):
                case nameof(ActionTypeIndex):
                case nameof(HasTurbo):
                case nameof(HasToggle):
                case nameof(ShiftModeIndex):
                case nameof(ShiftA):
                case nameof(ShiftB):
                case nameof(ShiftC):
                case nameof(ShiftD):
                    base.OnPropertyChanged(nameof(ActionTypeDisplayName));
                    base.OnPropertyChanged(nameof(TargetDisplayName));
                    base.OnPropertyChanged(nameof(TurboDisplayName));
                    base.OnPropertyChanged(nameof(ToggleDisplayName));
                    base.OnPropertyChanged(nameof(InputShiftDisplayName));
                    base.OnPropertyChanged(nameof(TouchpadActionTypeVisibility));
                    base.OnPropertyChanged(nameof(TouchpadAxisActionTypeVisibility));
                    base.OnPropertyChanged(nameof(UsesMovementHaptics));
                    base.OnPropertyChanged(nameof(Axis2ButtonVisibility));
                    base.OnPropertyChanged(nameof(Trigger2ButtonVisibility));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        // Property to block off updating to model in certain cases
        protected bool _updateToModel = true;
        protected static List<MappingTargetViewModel> _keyboardKeysTargets = [];

        public MappingViewModel(object value)
        {
            Value = value;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Targets, _collectionLock);

            // Lazy initialize to avoid re-creating target for Keyboard targets
            if (_keyboardKeysTargets.Count == 0)
            {
                foreach (KeyFlags key in KeyFlagsOrder.arr)
                {
                    _keyboardKeysTargets.Add(new MappingTargetViewModel
                    {
                        Tag = (VirtualKeyCode)key,
                        Content = EnumUtils.GetDescriptionFromEnumValue(key)
                    });
                }
            }

            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            VirtualManager.Initialized += VirtualManager_Initialized;

            // raise events
            if (VirtualManager.IsInitialized)
                VirtualManager_Initialized();

            // Send update event to Model
            PropertyChanged +=
                (s, e) =>
                {
                    if (_updateToModel && e.PropertyName is not null && !ExcludedUpdateProperties.Contains(e.PropertyName))
                        Update();
                };
        }

        private void VirtualManager_Initialized()
        {
            // manage events
            // which virtual output format to map it to
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

            // raise events
            VirtualManager_ControllerSelected(VirtualManager.HIDmode);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
                VirtualManager.Initialized -= VirtualManager_Initialized;
                VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;
            }

            base.Dispose(disposing);
        }

        private void VirtualManager_ControllerSelected(HIDmode hid) => ActionTypeChanged();

        protected abstract void ActionTypeChanged(ActionType? newActionType = null);
        protected abstract void TargetTypeChanged();
        protected abstract void Update();
        protected abstract void Delete();
        protected abstract void UpdateMapping(Layout layout);

        protected MappingTargetViewModel CreateTarget(object? tag, string content, bool isSupported = true)
        {
            return new MappingTargetViewModel
            {
                Tag = tag,
                Content = content,
                IsSupported = isSupported
            };
        }

        protected MappingTargetViewModel CreateUnsupportedTarget(object? tag, string content)
        {
            return CreateTarget(tag, $"{content} (Unavailable on current controller)", false);
        }

        protected void ReplaceTargets(List<MappingTargetViewModel> targets, MappingTargetViewModel? selectedTarget = null)
        {
            MappingTargetViewModel? resolvedTarget = selectedTarget ?? (targets.Count > 0 ? targets[0] : null);

            lock (_collectionLock)
            {
                Targets.Clear();
                foreach (var target in targets)
                    Targets.Add(target);
            }

            IsSupported = resolvedTarget?.IsSupported ?? true;
            SelectedTarget = resolvedTarget;
        }

        public virtual void SetAction(IActions newAction, bool updateToModel = true)
        {
            _selectedTarget = null;
            Action = newAction;

            _updateToModel = updateToModel;

            ActionTypeChanged(); // Includes full UI update

            // Reset update to model
            _updateToModel = true;
        }

        public virtual void Reset()
        {
            ActionTypeIndex = 0;
            SelectedTarget = null;
        }

        #endregion
    }
}
