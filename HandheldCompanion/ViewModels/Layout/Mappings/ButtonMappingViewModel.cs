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

        // default mapping can't be shifted
        public int ShiftIndex
        {
            get => Action is not null ? (int)Action.ShiftSlot : 0;
            set
            {
                if (Action is not null && value != ShiftIndex)
                {
                    Action.ShiftSlot = (ShiftSlot)value;
                    OnPropertyChanged(nameof(ShiftIndex));
                }
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

        public bool Turbo
        {
            get => Action is not null && Action.IsTurbo;
            set
            {
                if (Action is not null && value != Turbo)
                {
                    Action.IsTurbo = value;
                    OnPropertyChanged(nameof(Turbo));
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

        public bool Toggle
        {
            get => Action is not null && Action.IsToggle;
            set
            {
                if (Action is not null && value != Toggle)
                {
                    Action.IsToggle = value;
                    OnPropertyChanged(nameof(Toggle));
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

        public bool Interruptable
        {
            get => Action is not null && Action.Interruptable;
            set
            {
                if (Action is not null && value != Interruptable)
                {
                    Action.Interruptable = value;
                    OnPropertyChanged(nameof(Interruptable));
                }
            }
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
                foreach (ShiftSlot shiftSlot in Enum.GetValues<ShiftSlot>())
                {
                    switch (shiftSlot)
                    {
                        case ShiftSlot.None:
                        case ShiftSlot.Any:
                            continue;
                    }

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
                // Pick Any if matchingTargetVm is null
                SelectedTarget = matchingTargetVm ?? Targets.Last();
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
