using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Dummies;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class ButtonMappingViewModel : MappingViewModel
    {
        private static HashSet<MouseActionsType> _unsupportedMouseActionTypes =
        [
            MouseActionsType.Move,
            MouseActionsType.Scroll,
        ];

        #region Mapping Properties

        private int _pressTypeFallbackIndex = 0;
        public override int PressTypeIndex
        {
            get => Action is not null ? (int)Action.pressType : 0;
            set
            {
                // In case press type is changed when there's no action yet
                // Keep track of value to set PressTypeIndex when action is created
                _pressTypeFallbackIndex = value;

                if (Action is not null && value != PressTypeIndex)
                {
                    Action.pressType = (PressType)value;
                    OnPropertyChanged(nameof(PressTypeIndex));
                    OnPropertyChanged(nameof(PressTypeTooltip));
                    OnPropertyChanged(nameof(HasDuration));
                }
            }
        }

        public override string PressTypeTooltip
        {
            get
            {
                string key = $"LayoutPage_PressTypeTooltip{PressTypeIndex}";
                return Resources.ResourceManager.GetString(key) ?? string.Empty;
            }
        }

        public override float LongPressDelay
        {
            get => Action is not null ? Action.ActionTimer : 0;
            set
            {
                if (Action is not null && value != LongPressDelay)
                {
                    Action.ActionTimer = value;
                    OnPropertyChanged(nameof(LongPressDelay));
                }
            }
        }

        public override int ModifierIndex
        {
            get
            {
                if (Action is KeyboardActions keyboardAction)
                    return (int)keyboardAction.Modifiers;

                if (Action is MouseActions mouseAction)
                    return (int)mouseAction.Modifiers;

                return 0;
            }

            set
            {
                if (Action is not null && value != ModifierIndex)
                {
                    if (Action is KeyboardActions keyboardAction)
                        keyboardAction.Modifiers = (ModifierSet)value;

                    else if (Action is MouseActions mouseAction)
                        mouseAction.Modifiers = (ModifierSet)value;

                    OnPropertyChanged(nameof(ModifierIndex));
                }
            }
        }

        public override bool HasModifier
        {
            get
            {
                if (Action is not null)
                {
                    if (Action is KeyboardActions keyboardAction)
                    {
                        return true;
                    }
                    else if (Action is MouseActions mouseActions)
                    {
                        switch (mouseActions.MouseType)
                        {
                            case MouseActionsType.LeftButton:
                            case MouseActionsType.RightButton:
                            case MouseActionsType.MiddleButton:
                            case MouseActionsType.ScrollUp:
                            case MouseActionsType.ScrollDown:
                                return true;
                            case MouseActionsType.MoveTo:
                                return false;
                        }
                    }
                }

                return false;
            }
        }

        public override float TriggerOutput
        {
            get => (Action is TriggerActions triggerAction) ? triggerAction.motionThreshold : 0;
            set
            {
                if (Action is TriggerActions triggerAction && value != TriggerOutput)
                {
                    triggerAction.motionThreshold = value;
                    OnPropertyChanged(nameof(TriggerOutput));
                }
            }
        }

        public override int Axis2AxisInnerDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneInner : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInnerDeadzone)
                {
                    axisAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerInnerDeadzoneSize));
                }
            }
        }

        public override int Axis2AxisOuterDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneOuter : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOuterDeadzone)
                {
                    axisAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerOuterDeadzoneSize));
                }
            }
        }

        public override int Axis2AxisAntiDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisAntiDeadzone)
                {
                    axisAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
                    OnPropertyChanged(nameof(AxisVisualizerAntiDeadzoneSize));
                }
            }
        }

        public override int Axis2AxisOutputShapeIndex
        {
            get => (Action is AxisActions axisAction) ? (int)axisAction.OutputShape : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOutputShapeIndex)
                {
                    axisAction.OutputShape = (OutputShape)value;
                    OnPropertyChanged(nameof(Axis2AxisOutputShapeIndex));
                    OnPropertyChanged(nameof(AxisVisualizerDotX));
                    OnPropertyChanged(nameof(AxisVisualizerDotY));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateX));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateY));
                }
            }
        }

        public override bool Axis2AxisInvertHorizontal
        {
            get => (Action is AxisActions axisAction) && axisAction.InvertHorizontal;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInvertHorizontal)
                {
                    axisAction.InvertHorizontal = value;
                    OnPropertyChanged(nameof(Axis2AxisInvertHorizontal));
                }
            }
        }

        public override bool Axis2AxisInvertVertical
        {
            get => (Action is AxisActions axisAction) && axisAction.InvertVertical;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInvertVertical)
                {
                    axisAction.InvertVertical = value;
                    OnPropertyChanged(nameof(Axis2AxisInvertVertical));
                }
            }
        }

        public override int Button2AxisX
        {
            get => (Action is AxisActions axisAction) ? axisAction.ButtonX : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Button2AxisX)
                {
                    axisAction.ButtonX = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
                    OnPropertyChanged(nameof(Button2AxisX));
                    OnPropertyChanged(nameof(AxisVisualizerDotX));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateX));
                }
            }
        }

        public override int Button2AxisY
        {
            get => (Action is AxisActions axisAction) ? axisAction.ButtonY : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Button2AxisY)
                {
                    axisAction.ButtonY = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
                    OnPropertyChanged(nameof(Button2AxisY));
                    OnPropertyChanged(nameof(AxisVisualizerDotY));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateY));
                }
            }
        }

        public override Visibility Button2AxisVisibility => ActionTypeIndex == (int)ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;
        public override Visibility AxisInvertVisibility => Button2AxisVisibility;
        public override Visibility AxisVisualizerVisibility => ActionTypeIndex == (int)ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;

        public override double AxisVisualizerDotX => GetVisualizerDotOffset(GetVisualizerVector().X);
        public override double AxisVisualizerDotY => GetVisualizerDotOffset(GetVisualizerVector().Y);
        public override double AxisVisualizerDotTranslateX => AxisVisualizerDotX;
        public override double AxisVisualizerDotTranslateY => -AxisVisualizerDotY;
        public override double AxisVisualizerInnerDeadzoneSize => Axis2AxisInnerDeadzone * 2.0d;
        public override double AxisVisualizerOuterDeadzoneSize => Math.Max(0.0d, 200.0d - Axis2AxisOuterDeadzone * 2.0d);
        public override double AxisVisualizerAntiDeadzoneSize => Axis2AxisAntiDeadzone * 2.0d;

        private Vector2 GetVisualizerVector()
        {
            if (Action is not AxisActions axisAction)
                return Vector2.Zero;

            Vector2 vector = new(Button2AxisX, Button2AxisY);

            return axisAction.OutputShape switch
            {
                OutputShape.Circle => InputUtils.ImproveCircularity(vector),
                OutputShape.Cross => InputUtils.ImproveCircularity(InputUtils.CrossDeadzoneMapping(vector, axisAction.AxisDeadZoneInner, axisAction.AxisDeadZoneOuter)),
                OutputShape.Square => InputUtils.ImproveSquare(vector),
                _ => vector,
            };
        }

        private static double GetVisualizerDotOffset(float value)
        {
            double normalized = Math.Clamp(value / (double)short.MaxValue, -1.0d, 1.0d);
            return normalized * 94.0d;
        }

        // Trigger output should only be visible for Button -> Trigger mappings
        public override Visibility TriggerOutputVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                return currentActionType == ActionType.Trigger ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public override int HapticModeIndex
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

        public override int HapticStrengthIndex
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

        public override double Button2MouseToX
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToX : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Button2MouseToX)
                {
                    mouseAction.MoveToX = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Button2MouseToX));
                }
            }
        }

        public override double Button2MouseToY
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToY : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Button2MouseToY)
                {
                    mouseAction.MoveToY = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Button2MouseToY));
                }
            }
        }

        public override bool Button2MouseRestore
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToPrevious : false;
            set
            {
                if (Action is MouseActions mouseAction && value != Button2MouseRestore)
                {
                    mouseAction.MoveToPrevious = value;
                    OnPropertyChanged(nameof(Button2MouseRestore));
                }
            }
        }

        public override Visibility Button2MouseTo
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Mouse && SelectedTarget != null)
                {
                    if (SelectedTarget.Tag is not MouseActionsType mouseAction)
                        return Visibility.Collapsed;

                    return mouseAction == MouseActionsType.MoveTo ? Visibility.Visible : Visibility.Collapsed;
                }

                return Visibility.Collapsed;
            }
        }

        public override Visibility TouchpadSettingsVisibility =>
            ActionTypeIndex == (int)ActionType.Button && Action is TouchpadActions
                ? Visibility.Visible : Visibility.Collapsed;
        public override Visibility TouchpadSwipeSettingsVisibility =>
            ActionTypeIndex == (int)ActionType.Button && Action is TouchpadActions { Button: ButtonFlags.TouchpadSwipe }
                ? Visibility.Visible : Visibility.Collapsed;
        public int TouchpadMaxX => DS4Touch.TOUCHPAD_WIDTH - 1;
        public int TouchpadMaxY => DS4Touch.TOUCHPAD_HEIGHT - 1;
        public double TouchpadMaxDuration => TouchpadActions.MaximumSwipeDuration;
        public string TouchpadCoordinateRangeDescription => $"DualSense coordinates: X 0–{TouchpadMaxX}, Y 0–{TouchpadMaxY}";
        public int TouchpadX { get => Action is TouchpadActions a ? a.X : 0; set { if (Action is TouchpadActions a) { a.X = value; OnPropertyChanged(nameof(TouchpadX)); } } }
        public int TouchpadY { get => Action is TouchpadActions a ? a.Y : 0; set { if (Action is TouchpadActions a) { a.Y = value; OnPropertyChanged(nameof(TouchpadY)); } } }
        public int TouchpadEndX { get => Action is TouchpadActions a ? a.EndX : 0; set { if (Action is TouchpadActions a) { a.EndX = value; OnPropertyChanged(nameof(TouchpadEndX)); } } }
        public int TouchpadEndY { get => Action is TouchpadActions a ? a.EndY : 0; set { if (Action is TouchpadActions a) { a.EndY = value; OnPropertyChanged(nameof(TouchpadEndY)); } } }
        public double TouchpadSwipeDuration { get => Action is TouchpadActions a ? a.SwipeDuration : TouchpadActions.DefaultSwipeDuration; set { if (Action is TouchpadActions a) { a.SwipeDuration = (float)value; OnPropertyChanged(nameof(TouchpadSwipeDuration)); } } }

        public override void OnPropertyChanged(string? propertyName)
        {
            switch (propertyName)
            {
                case "":
                case nameof(SelectedTarget):
                case nameof(ActionTypeIndex):
                    OnPropertyChanged(nameof(HasModifier));
                    OnPropertyChanged(nameof(Button2MouseTo));
                    OnPropertyChanged(nameof(MouseSettingsSectionVisibility));
                    OnPropertyChanged(nameof(AxisSettingsSectionVisibility));
                    OnPropertyChanged(nameof(TriggerSettingsSectionVisibility));
                    OnPropertyChanged(nameof(TimingSettingsSectionVisibility));
                    OnPropertyChanged(nameof(GeneralActionVisibility));
                    OnPropertyChanged(nameof(Button2AxisVisibility));
                    OnPropertyChanged(nameof(AxisInvertVisibility));
                    OnPropertyChanged(nameof(AxisVisualizerVisibility));
                    OnPropertyChanged(nameof(AxisVisualizerDotX));
                    OnPropertyChanged(nameof(AxisVisualizerDotY));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateX));
                    OnPropertyChanged(nameof(AxisVisualizerDotTranslateY));
                    OnPropertyChanged(nameof(AxisVisualizerInnerDeadzoneSize));
                    OnPropertyChanged(nameof(AxisVisualizerOuterDeadzoneSize));
                    OnPropertyChanged(nameof(AxisVisualizerAntiDeadzoneSize));
                    OnPropertyChanged(nameof(TouchpadSettingsVisibility));
                    OnPropertyChanged(nameof(TouchpadSwipeSettingsVisibility));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        public override bool HasDuration => PressTypeIndex != (int)PressType.Short;

        #endregion

        private ButtonStackViewModel _parentStack;
        public ButtonStackViewModel ParentStack => _parentStack;

        public ICommand ButtonCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        public ButtonMappingViewModel(ButtonStackViewModel parentStack, ButtonFlags button) : base(button)
        {
            _parentStack = parentStack;

            ButtonCommand = new DelegateCommand(() =>
            {
                if (Action is not null) Delete();
                _parentStack.RemoveMapping(this);
            });

            OpenSettingsCommand = new DelegateCommand(() =>
            {
                // Navigate to LayoutItemPage
                if (MainWindow.layoutItemPage is not null)
                {
                    MainWindow.layoutItemPage.SetMapping(this);
                    MainWindow.NavView_Navigate(MainWindow.layoutItemPage);
                }
            });
        }

        protected override void ActionTypeChanged(ActionType? newActionType = null)
        {
            var actionType = newActionType ?? (ActionType)ActionTypeIndex;
            if (actionType == ActionType.Disabled)
            {
                IsSupported = true;
                if (Action is not null) Delete();
                SelectedTarget = null;
                OnPropertyChanged(string.Empty);
                return;
            }

            // get current controller
            IController controller = ControllerManager.GetDefault(true);

            // Build Targets
            List<MappingTargetViewModel> targets = new List<MappingTargetViewModel>();

            PressType fallbackPressType = (PressType)_pressTypeFallbackIndex;

            if (actionType == ActionType.Button)
            {
                bool preserveMissingTarget = Action is ButtonActions;
                if (!preserveMissingTarget)
                {
                    Action = new ButtonActions()
                    {
                        pressType = fallbackPressType,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var button in controller.GetTargetButtons())
                {
                    var mappingTargetVm = CreateTarget(button, controller.GetButtonName(button));
                    targets.Add(mappingTargetVm);

                    if (button == ((ButtonActions)Action).Button)
                        matchingTargetVm = mappingTargetVm;
                }

                // Parameterized touchpad gestures are mapping actions rather than
                // physical controller buttons. Expose them only in the button editor.
                if (controller is DummyDualSenseController)
                {
                    foreach (var button in TouchpadActions.Targets)
                    {
                        var mappingTargetVm = CreateTarget(button, controller.GetButtonName(button));
                        targets.Add(mappingTargetVm);

                        if (button == ((ButtonActions)Action).Button)
                            matchingTargetVm = mappingTargetVm;
                    }
                }

                if (matchingTargetVm is null && preserveMissingTarget)
                {
                    matchingTargetVm = CreateUnsupportedTarget(((ButtonActions)Action).Button,
                        controller.GetButtonName(((ButtonActions)Action).Button));
                    targets.Add(matchingTargetVm);
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Keyboard)
            {
                if (Action is null || Action is not KeyboardActions)
                {
                    Action = new KeyboardActions()
                    {
                        pressType = fallbackPressType,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                targets.AddRange(_keyboardKeysTargets);
                ReplaceTargets(targets, _keyboardKeysTargets.FirstOrDefault(e => Equals(e.Tag, ((KeyboardActions)Action).Key)));
            }
            else if (actionType == ActionType.Joystick)
            {
                bool preserveMissingTarget = Action is AxisActions;
                if (!preserveMissingTarget)
                {
                    Action = new AxisActions()
                    {
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var axis in controller.GetTargetAxis())
                {
                    var mappingTargetVm = CreateTarget(axis, controller.GetAxisName(axis));
                    targets.Add(mappingTargetVm);

                    if (axis == ((AxisActions)Action).Axis)
                        matchingTargetVm = mappingTargetVm;
                }

                if (matchingTargetVm is null && preserveMissingTarget)
                {
                    matchingTargetVm = CreateUnsupportedTarget(((AxisActions)Action).Axis,
                        controller.GetAxisName(((AxisActions)Action).Axis));
                    targets.Add(matchingTargetVm);
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                {
                    Action = new MouseActions()
                    {
                        pressType = fallbackPressType,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var mouseType in Enum.GetValues<MouseActionsType>().Except(_unsupportedMouseActionTypes))
                {
                    var mappingTargetVm = CreateTarget(mouseType, EnumUtils.GetDescriptionFromEnumValue(mouseType));
                    targets.Add(mappingTargetVm);

                    if (mouseType == ((MouseActions)Action).MouseType)
                        matchingTargetVm = mappingTargetVm;
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Trigger)
            {
                bool preserveMissingTarget = Action is TriggerActions;
                if (!preserveMissingTarget)
                {
                    Action = new TriggerActions()
                    {
                        motionThreshold = 125,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var axis in controller.GetTargetTriggers())
                {
                    var mappingTargetVm = CreateTarget(axis, controller.GetAxisName(axis));
                    targets.Add(mappingTargetVm);

                    if (axis == ((TriggerActions)Action).Axis)
                        matchingTargetVm = mappingTargetVm;
                }

                if (matchingTargetVm is null && preserveMissingTarget)
                {
                    matchingTargetVm = CreateUnsupportedTarget(((TriggerActions)Action).Axis,
                        controller.GetAxisName(((TriggerActions)Action).Axis));
                    targets.Add(matchingTargetVm);
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Shift)
            {
                if (Action is null || Action is not ShiftActions)
                    Action = new ShiftActions();

                MappingTargetViewModel? matchingTargetVm = null;
                // Only show individual shift slots (A, B, C, D), not None or combined values
                foreach (ShiftSlot shiftSlot in new[] { ShiftSlot.ShiftA, ShiftSlot.ShiftB, ShiftSlot.ShiftC, ShiftSlot.ShiftD })
                {
                    MappingTargetViewModel mappingTargetVm = CreateTarget(shiftSlot, EnumUtils.GetDescriptionFromEnumValue(shiftSlot));
                    targets.Add(mappingTargetVm);

                    if (shiftSlot == ((ShiftActions)Action).ActivationSlot)
                        matchingTargetVm = mappingTargetVm;
                }

                ReplaceTargets(targets, matchingTargetVm);
            }
            else if (actionType == ActionType.Inherit)
            {
                if (Action is null || Action is not InheritActions)
                    Action = new InheritActions();

                ReplaceTargets(targets);
            }

            // Refresh mapping
            OnPropertyChanged(string.Empty);
        }

        protected override void TargetTypeChanged()
        {
            if (Action is null || SelectedTarget is null)
                return;

            switch (Action.actionType)
            {
                case ActionType.Button:
                    if (SelectedTarget.Tag is ButtonFlags buttonFlags)
                    {
                        bool isTouchpadAction = TouchpadActions.IsTouchpadTarget(buttonFlags);

                        if (isTouchpadAction && Action is not TouchpadActions)
                        {
                            IActions previous = Action;
                            Action = new TouchpadActions(buttonFlags);
                            Action.CopyConfigurationFrom(previous);
                        }
                        else if (!isTouchpadAction && Action is TouchpadActions)
                        {
                            IActions previous = Action;
                            Action = new ButtonActions(buttonFlags);
                            Action.CopyConfigurationFrom(previous);
                        }
                        else
                            ((ButtonActions)Action).Button = buttonFlags;

                        OnPropertyChanged(string.Empty);
                    }
                    break;

                case ActionType.Keyboard:
                    if (SelectedTarget.Tag is VirtualKeyCode virtualKeyCode)
                        ((KeyboardActions)Action).Key = virtualKeyCode;
                    break;

                case ActionType.Joystick:
                    if (SelectedTarget.Tag is AxisLayoutFlags axisLayoutFlags)
                        ((AxisActions)Action).Axis = axisLayoutFlags;
                    break;

                case ActionType.Mouse:
                    if (SelectedTarget.Tag is MouseActionsType mouseActionsType)
                        ((MouseActions)Action).MouseType = mouseActionsType;
                    break;

                case ActionType.Shift:
                    if (SelectedTarget.Tag is ShiftSlot shiftSlot)
                        ((ShiftActions)Action).ActivationSlot = shiftSlot;
                    break;

                case ActionType.Trigger:
                    if (SelectedTarget.Tag is AxisLayoutFlags triggerAxisLayoutFlags)
                        ((TriggerActions)Action).Axis = triggerAxisLayoutFlags;
                    break;
            }
        }

        protected override void Update()
        {
            _parentStack.UpdateFromMapping();
        }

        protected override void Delete()
        {
            Action = null;
            _parentStack.UpdateFromMapping();
        }

        // Done from ButtonStack
        protected override void UpdateMapping(Layout layout)
        {
        }
    }
}
