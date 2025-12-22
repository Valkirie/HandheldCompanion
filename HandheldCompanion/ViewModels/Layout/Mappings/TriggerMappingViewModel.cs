using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class TriggerMappingViewModel : MappingViewModel
    {
        private static readonly HashSet<MouseActionsType> _unsupportedMouseActionTypes =
        [
            MouseActionsType.Move,
            MouseActionsType.Scroll
        ];

        public int Trigger2TriggerInnerDeadzone
        {
            get => (Action is TriggerActions triggerAction) ? triggerAction.AxisDeadZoneInner : 0;
            set
            {
                if (Action is TriggerActions triggerAction && value != Trigger2TriggerInnerDeadzone)
                {
                    triggerAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Trigger2TriggerInnerDeadzone));
                }
            }
        }

        public int Trigger2TriggerOuterDeadzone
        {
            get => (Action is TriggerActions triggerAction) ? triggerAction.AxisDeadZoneOuter : 0;
            set
            {
                if (Action is TriggerActions triggerAction && value != Trigger2TriggerOuterDeadzone)
                {
                    triggerAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Trigger2TriggerOuterDeadzone));
                }
            }
        }

        public int Trigger2TriggerAntiDeadzone
        {
            get => (Action is TriggerActions triggerAction) ? triggerAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is TriggerActions triggerAction && value != Trigger2TriggerAntiDeadzone)
                {
                    triggerAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Trigger2TriggerAntiDeadzone));
                }
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

        private TriggerStackViewModel _parentStack;

        public ICommand ButtonCommand { get; private set; }

        public TriggerMappingViewModel(TriggerStackViewModel parentStack, AxisLayoutFlags value) : base(value)
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

            if (actionType == ActionType.Button)
            {
                if (Action is null || Action is not ButtonActions)
                    Action = new ButtonActions() { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

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
                    Action = new KeyboardActions { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                    Action = new MouseActions { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

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
                    Action = new TriggerActions();

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
                    Action = new ShiftActions(ShiftSlot.ShiftA) { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

                MappingTargetViewModel? matchingTargetVm = null;
                // Only show individual shift slots (A, B, C, D), not None or combined values
                foreach (ShiftSlot shiftSlot in new[] { ShiftSlot.ShiftA, ShiftSlot.ShiftB, ShiftSlot.ShiftC, ShiftSlot.ShiftD })
                {
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = shiftSlot,
                        Content = EnumUtils.GetDescriptionFromEnumValue(shiftSlot)
                    };
                    targets.Add(mappingTargetVm);

                    if (shiftSlot == ((ShiftActions)Action).ShiftSlot)
                        matchingTargetVm = mappingTargetVm;
                }

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

                case ActionType.Trigger:
                    ((TriggerActions)Action).Axis = (AxisLayoutFlags)SelectedTarget.Tag;
                    break;

                case ActionType.Mouse:
                    ((MouseActions)Action).MouseType = (MouseActionsType)SelectedTarget.Tag;
                    break;

                case ActionType.Shift:
                    ((ShiftActions)Action).ShiftSlot = (ShiftSlot)SelectedTarget.Tag;
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

        // Done from AxisStack
        protected override void UpdateMapping(Layout layout)
        {
        }
    }
}
