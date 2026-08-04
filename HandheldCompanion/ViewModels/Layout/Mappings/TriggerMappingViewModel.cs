using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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

        public override bool IsTriggerMapping => true;

        public override int Trigger2TriggerInnerDeadzone
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

        public override int Trigger2TriggerOuterDeadzone
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

        public override int Trigger2TriggerAntiDeadzone
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

        // Trigger deadzone visibility - only visible when Trigger -> Trigger/Axis
        public override Visibility TriggerDeadzoneVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                // Show for Trigger -> Trigger or Trigger -> Joystick (Axis)
                return (currentActionType == ActionType.Trigger || currentActionType == ActionType.Joystick)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
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

        private TriggerStackViewModel _parentStack;
        public TriggerStackViewModel ParentStack => _parentStack;

        public ICommand ButtonCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        public TriggerMappingViewModel(TriggerStackViewModel parentStack, AxisLayoutFlags value) : base(value)
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

            if (actionType == ActionType.Button)
            {
                bool preserveMissingTarget = Action is ButtonActions;
                if (!preserveMissingTarget)
                    Action = new ButtonActions() { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var button in controller.GetTargetButtons())
                {
                    var mappingTargetVm = CreateTarget(button, controller.GetButtonName(button));
                    targets.Add(mappingTargetVm);

                    if (button == ((ButtonActions)Action).Button)
                        matchingTargetVm = mappingTargetVm;
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
                    Action = new KeyboardActions
                    {
                        motionThreshold = Gamepad.TriggerThreshold,
                        motionDirection = DeflectionDirection.Up,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                targets.AddRange(_keyboardKeysTargets);
                ReplaceTargets(targets, _keyboardKeysTargets.FirstOrDefault(e => Equals(e.Tag, ((KeyboardActions)Action).Key)));
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                {
                    Action = new MouseActions
                    {
                        motionThreshold = Gamepad.TriggerThreshold,
                        motionDirection = DeflectionDirection.Up,
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
                    Action = new ShiftActions() { motionThreshold = Gamepad.TriggerThreshold, motionDirection = DeflectionDirection.Up };

                MappingTargetViewModel? matchingTargetVm = null;
                // Only show individual shift slots (A, B, C, D), not None or combined values
                foreach (ShiftSlot shiftSlot in new[] { ShiftSlot.ShiftA, ShiftSlot.ShiftB, ShiftSlot.ShiftC, ShiftSlot.ShiftD })
                {
                    var mappingTargetVm = CreateTarget(shiftSlot, EnumUtils.GetDescriptionFromEnumValue(shiftSlot));
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
            OnPropertyChanged(nameof(TriggerDeadzoneVisibility));
        }

        public override void OnPropertyChanged(string? propertyName)
        {
            switch (propertyName)
            {
                case "SelectedTarget":
                case "ActionTypeIndex":
                    OnPropertyChanged(nameof(TriggerDeadzoneVisibility));
                    OnPropertyChanged(nameof(TriggerSettingsSectionVisibility));
                    OnPropertyChanged(nameof(GeneralActionVisibility));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        protected override void TargetTypeChanged()
        {
            if (Action is null || SelectedTarget is null)
                return;

            switch (Action.actionType)
            {
                case ActionType.Button:
                    if (SelectedTarget.Tag is ButtonFlags buttonFlags)
                        ((ButtonActions)Action).Button = buttonFlags;
                    break;

                case ActionType.Keyboard:
                    if (SelectedTarget.Tag is VirtualKeyCode virtualKeyCode)
                        ((KeyboardActions)Action).Key = virtualKeyCode;
                    break;

                case ActionType.Trigger:
                    if (SelectedTarget.Tag is AxisLayoutFlags axisLayoutFlags)
                        ((TriggerActions)Action).Axis = axisLayoutFlags;
                    break;

                case ActionType.Mouse:
                    if (SelectedTarget.Tag is MouseActionsType mouseActionsType)
                        ((MouseActions)Action).MouseType = mouseActionsType;
                    break;

                case ActionType.Shift:
                    if (SelectedTarget.Tag is ShiftSlot shiftSlot)
                        ((ShiftActions)Action).ActivationSlot = shiftSlot;
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
