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
                    Action = new ButtonActions() { motionThreshold = Gamepad.TriggerThreshold, motionDirection = MotionDirection.Up };

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
                    Action = new KeyboardActions { motionThreshold = Gamepad.TriggerThreshold, motionDirection = MotionDirection.Up };

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                    Action = new MouseActions { motionThreshold = Gamepad.TriggerThreshold, motionDirection = MotionDirection.Up };

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
