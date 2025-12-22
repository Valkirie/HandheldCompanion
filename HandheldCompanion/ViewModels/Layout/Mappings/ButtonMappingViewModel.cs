using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public int PressTypeIndex
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

        public string PressTypeTooltip
        {
            get
            {
                string key = $"LayoutPage_PressTypeTooltip{PressTypeIndex}";
                return Resources.ResourceManager.GetString(key) ?? string.Empty;
            }
        }

        // Shift mode: 0 = Disabled on shift, 1 = Always enabled, 2 = Enabled on specific shifts
        public int ShiftModeIndex
        {
            get
            {
                if (Action is null) return 1; // Default to always enabled
                if (Action.ShiftSlot.HasFlag(ShiftSlot.Any)) return 1; // Always enabled
                if (Action.ShiftSlot == ShiftSlot.None) return 0; // Disabled on shift
                return 2; // Specific shifts selected
            }
            set
            {
                if (Action is null || value == ShiftModeIndex) return;

                switch (value)
                {
                    case 0: // Disabled on shift
                        Action.ShiftSlot = ShiftSlot.None;
                        break;
                    case 1: // Always enabled
                        Action.ShiftSlot = ShiftSlot.Any;
                        break;
                    case 2: // Enabled on shift - default to ShiftA if nothing selected
                        Action.ShiftSlot = ShiftSlot.ShiftA;
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

        public bool ShowShiftSelection => ShiftModeIndex == 2;

        public bool ShiftA
        {
            get => Action is not null && Action.ShiftSlot.HasFlag(ShiftSlot.ShiftA);
            set
            {
                if (Action is null || value == ShiftA) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftA
                    : Action.ShiftSlot & ~ShiftSlot.ShiftA;
                OnPropertyChanged(nameof(ShiftA));
            }
        }

        public bool ShiftB
        {
            get => Action is not null && Action.ShiftSlot.HasFlag(ShiftSlot.ShiftB);
            set
            {
                if (Action is null || value == ShiftB) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftB
                    : Action.ShiftSlot & ~ShiftSlot.ShiftB;
                OnPropertyChanged(nameof(ShiftB));
            }
        }

        public bool ShiftC
        {
            get => Action is not null && Action.ShiftSlot.HasFlag(ShiftSlot.ShiftC);
            set
            {
                if (Action is null || value == ShiftC) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftC
                    : Action.ShiftSlot & ~ShiftSlot.ShiftC;
                OnPropertyChanged(nameof(ShiftC));
            }
        }

        public bool ShiftD
        {
            get => Action is not null && Action.ShiftSlot.HasFlag(ShiftSlot.ShiftD);
            set
            {
                if (Action is null || value == ShiftD) return;
                Action.ShiftSlot = value
                    ? Action.ShiftSlot | ShiftSlot.ShiftD
                    : Action.ShiftSlot & ~ShiftSlot.ShiftD;
                OnPropertyChanged(nameof(ShiftD));
            }
        }

        public float LongPressDelay
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

        public int ModifierIndex
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

        public bool HasModifier
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

        public float TriggerOutput
        {
            get => Action is not null ? Action.motionThreshold : 0;
            set
            {
                if (Action is not null && value != TriggerOutput)
                {
                    Action.motionThreshold = value;
                    OnPropertyChanged(nameof(TriggerOutput));
                }
            }
        }

        public int HapticModeIndex
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

        public int HapticStrengthIndex
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

        public double Button2MouseToX
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

        public double Button2MouseToY
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

        public bool Button2MouseRestore
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

        public Visibility Button2MouseTo
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Mouse && SelectedTarget != null)
                {
                    MouseActionsType mouseAction = (MouseActionsType)SelectedTarget.Tag;
                    return mouseAction == MouseActionsType.MoveTo ? Visibility.Visible : Visibility.Collapsed;
                }

                return Visibility.Collapsed;
            }
        }

        public override void OnPropertyChanged(string propertyName)
        {
            switch (propertyName)
            {
                case "SelectedTarget":
                case "ActionTypeIndex":
                    OnPropertyChanged(nameof(HasModifier));
                    OnPropertyChanged(nameof(Button2MouseTo));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        public bool HasDuration => PressTypeIndex != (int)PressType.Short;

        #endregion

        private ButtonStackViewModel _parentStack;

        public ICommand ButtonCommand { get; private set; }

        public ButtonMappingViewModel(ButtonStackViewModel parentStack, ButtonFlags button) : base(button)
        {
            _parentStack = parentStack;

            ButtonCommand = new DelegateCommand(() =>
            {
                if (Action is not null) Delete();
                _parentStack.RemoveMapping(this);
            });
        }

        protected override void ActionTypeChanged(ActionType? newActionType = null)
        {
            var actionType = newActionType ?? (ActionType)ActionTypeIndex;
            if (actionType == ActionType.Disabled)
            {
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
                if (Action is null || Action is not ButtonActions)
                    Action = new ButtonActions() { pressType = fallbackPressType };

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var button in controller.GetTargetButtons())
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = button,
                        Content = controller.GetButtonName(button)
                    };
                    targets.Add(mappingTargetVm);

                    if (button == ((ButtonActions)Action).Button)
                        matchingTargetVm = mappingTargetVm;
                }

                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Keyboard)
            {
                if (Action is null || Action is not KeyboardActions)
                    Action = new KeyboardActions() { pressType = fallbackPressType };

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                    Action = new MouseActions() { pressType = fallbackPressType };

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var mouseType in Enum.GetValues<MouseActionsType>().Except(_unsupportedMouseActionTypes))
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = mouseType,
                        Content = EnumUtils.GetDescriptionFromEnumValue(mouseType)
                    };
                    targets.Add(mappingTargetVm);

                    if (mouseType == ((MouseActions)Action).MouseType)
                        matchingTargetVm = mappingTargetVm;
                }

                // Update list and selected target
                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Trigger)
            {
                if (Action is null || Action is not TriggerActions)
                    Action = new TriggerActions() { motionThreshold = 125 };

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var axis in controller.GetTargetTriggers())
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = axis,
                        Content = controller.GetAxisName(axis)
                    };
                    targets.Add(mappingTargetVm);

                    if (axis == ((TriggerActions)Action).Axis)
                        matchingTargetVm = mappingTargetVm;
                }

                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Shift)
            {
                if (Action is null || Action is not ShiftActions)
                    Action = new ShiftActions(ShiftSlot.ShiftA);

                MappingTargetViewModel? matchingTargetVm = null;
                // Only show individual shift slots (A, B, C, D), not None or combined values
                foreach (ShiftSlot shiftSlot in new[] { ShiftSlot.ShiftA, ShiftSlot.ShiftB, ShiftSlot.ShiftC, ShiftSlot.ShiftD })
                {
                    MappingTargetViewModel mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = shiftSlot,
                        Content = EnumUtils.GetDescriptionFromEnumValue(shiftSlot)
                    };
                    targets.Add(mappingTargetVm);

                    if (shiftSlot == ((ShiftActions)Action).ShiftSlot)
                        matchingTargetVm = mappingTargetVm;
                }

                // Update list and selected target
                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Inherit)
            {
                if (Action is null || Action is not InheritActions)
                    Action = new InheritActions();

                // Update list and selected target
                Targets.Clear();
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
                    ((ButtonActions)Action).Button = (ButtonFlags)SelectedTarget.Tag;
                    break;

                case ActionType.Keyboard:
                    ((KeyboardActions)Action).Key = (VirtualKeyCode)SelectedTarget.Tag;
                    break;

                case ActionType.Mouse:
                    ((MouseActions)Action).MouseType = (MouseActionsType)SelectedTarget.Tag;
                    break;

                case ActionType.Shift:
                    ((ShiftActions)Action).ShiftSlot = (ShiftSlot)SelectedTarget.Tag;
                    break;

                case ActionType.Trigger:
                    ((TriggerActions)Action).Axis = (AxisLayoutFlags)SelectedTarget.Tag;
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
