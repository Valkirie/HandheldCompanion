using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                }
            }
        }

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

        // Axis direction properties (for Axis2Button) - default to false
        // These should only be visible for AxisMappingViewModel, not ButtonMappingViewModel
        public virtual bool IsUp { get => false; set { } }
        public virtual bool IsDown { get => false; set { } }
        public virtual bool IsLeft { get => false; set { } }
        public virtual bool IsRight { get => false; set { } }

        // Property to check if this is an Axis mapping (for visibility conditions)
        public virtual bool IsAxisMapping => false;

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

        // Visibility for Axis invert properties - only Axis mappings
        public virtual Visibility AxisInvertVisibility => Visibility.Collapsed;
        public virtual Visibility Button2AxisVisibility => Visibility.Collapsed;
        public virtual Visibility AxisVisualizerVisibility => Visibility.Collapsed;
        public virtual double AxisVisualizerDotX => 0.0d;
        public virtual double AxisVisualizerDotY => 0.0d;
        public virtual double AxisVisualizerDotTranslateX => 0.0d;
        public virtual double AxisVisualizerDotTranslateY => 0.0d;
        public virtual double AxisVisualizerInnerDeadzoneSize => 0.0d;
        public virtual double AxisVisualizerOuterDeadzoneSize => 0.0d;
        public virtual double AxisVisualizerAntiDeadzoneSize => 0.0d;

        // Trigger to Trigger/Axis deadzone properties - default to 0
        // Should only be visible for Trigger -> Trigger/Axis mappings
        public virtual int Trigger2TriggerInnerDeadzone { get => 0; set { } }
        public virtual int Trigger2TriggerOuterDeadzone { get => 0; set { } }
        public virtual int Trigger2TriggerAntiDeadzone { get => 0; set { } }

        // Visibility for Trigger deadzone properties - only Trigger -> Trigger/Axis
        public virtual Visibility TriggerDeadzoneVisibility => Visibility.Collapsed;

        // Axis to Mouse properties (for Mouse actions) - default to 0/false
        public virtual int Axis2MousePointerSpeed { get => 0; set { } }
        public virtual float Axis2MouseAcceleration { get => 0; set { } }
        public virtual int Axis2MouseDeadzone { get => 0; set { } }
        public virtual bool Axis2MouseFiltering { get => false; set { } }
        public virtual float Axis2MouseFilterCutoff { get => 0; set { } }

        // Axis visibility properties - default to Collapsed
        public virtual Visibility Axis2TouchpadVisibility => Visibility.Collapsed;
        public virtual Visibility Axis2JoystickVisibility => Visibility.Collapsed;

        // Axis Direction and Threshold should only be visible for Axis mappings converting to Button
        public virtual Visibility AxisDirectionVisibility => Visibility.Collapsed;
        public virtual Visibility AxisThresholdVisibility => Visibility.Collapsed;

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
                    currentActionType == ActionType.Shift || currentActionType == ActionType.Joystick)
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

        // Modifier properties (for Keyboard/Mouse actions) - default to 0/false
        public virtual int ModifierIndex { get => 0; set { } }
        public virtual bool HasModifier => false;

        // Press type properties (for Button actions) - default to 0/false
        public virtual int PressTypeIndex { get => 0; set { } }
        public virtual string PressTypeTooltip => string.Empty;
        public virtual bool HasDuration => false;
        public virtual float LongPressDelay { get => 0; set { } }

        // Haptic properties - default to 0
        public virtual int HapticModeIndex
        {
            get => Action is not null ? (int)Action.HapticMode : 0;
            set
            {
                if (Action is not null && value != HapticModeIndex)
                {
                    Action.HapticMode = (HapticMode)value;
                    OnPropertyChanged(nameof(HapticModeIndex));
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
    }
}
