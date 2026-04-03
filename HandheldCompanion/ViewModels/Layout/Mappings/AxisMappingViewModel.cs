using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
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
    public class AxisMappingViewModel : MappingViewModel
    {
        #region Axis Action Properties

        // Shift mode: 0 = Disabled on shift, 1 = Always enabled, 2 = Enabled on shift (strict), 3 = Enabled on shift (any)
        public override int ShiftModeIndex
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

        public override bool ShowShiftSelection => ShiftModeIndex == 2 || ShiftModeIndex == 3;

        public override bool ShiftA
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

        public override bool ShiftB
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

        public override bool ShiftC
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

        public override bool ShiftD
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

        #region Axis2Button
        public int Axis2ButtonDirection
        {
            get => (int)((Action is IActions iActions) ? iActions.motionDirection : 0);
            set
            {
                if (Action is IActions iActions && value != Axis2ButtonDirection)
                {
                    iActions.motionDirection = (DeflectionDirection)value;
                    OnPropertyChanged(nameof(Axis2ButtonDirection));

                    // Cascade notifications to dependent properties
                    OnPropertyChanged(nameof(IsLeft));
                    OnPropertyChanged(nameof(IsRight));
                    OnPropertyChanged(nameof(IsUp));
                    OnPropertyChanged(nameof(IsDown));
                }
            }
        }

        public override bool IsLeft
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Left);
            set
            {
                if (value != IsLeft)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Left
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Left;
                }
            }
        }

        public override bool IsRight
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Right);
            set
            {
                if (value != IsRight)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Right
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Right;
                }
            }
        }

        public override bool IsUp
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Up);
            set
            {
                if (value != IsUp)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Up
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Up;
                }
            }
        }

        public override bool IsDown
        {
            get => ((DeflectionDirection)Axis2ButtonDirection).HasFlag(DeflectionDirection.Down);
            set
            {
                if (value != IsDown)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)DeflectionDirection.Down
                        : Axis2ButtonDirection & ~(int)DeflectionDirection.Down;
                }
            }
        }
        #endregion

        public override int Axis2AxisInnerDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneInner : (Action is TriggerActions triggerAction) ? triggerAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInnerDeadzone)
                {
                    axisAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisInnerDeadzone)
                {
                    triggerAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                }
            }
        }

        public override int Axis2AxisOuterDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneOuter : (Action is TriggerActions triggerAction) ? triggerAction.AxisDeadZoneOuter : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOuterDeadzone)
                {
                    axisAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisOuterDeadzone)
                {
                    triggerAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                }
            }
        }

        public override int Axis2AxisAntiDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisAntiDeadZone : (Action is TriggerActions triggerAction) ? triggerAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisAntiDeadzone)
                {
                    axisAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
                }
                else if (Action is TriggerActions triggerAction && value != Axis2AxisAntiDeadzone)
                {
                    triggerAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
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
                }
            }
        }

        public override bool Axis2AxisInvertHorizontal
        {
            get => (Action is AxisActions axisAction) ? axisAction.InvertHorizontal : false;
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
            get => (Action is AxisActions axisAction) ? axisAction.InvertVertical : false;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInvertVertical)
                {
                    axisAction.InvertVertical = value;
                    OnPropertyChanged(nameof(Axis2AxisInvertVertical));
                }
            }
        }

        #endregion

        #region Mouse Action Properties

        public override int Axis2MousePointerSpeed
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Sensivity : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MousePointerSpeed)
                {
                    mouseAction.Sensivity = value;
                    OnPropertyChanged(nameof(Axis2MousePointerSpeed));
                }
            }
        }

        public override int Axis2MouseDeadzone
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Deadzone : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseDeadzone)
                {
                    mouseAction.Deadzone = value;
                    OnPropertyChanged(nameof(Axis2MouseDeadzone));
                }
            }
        }

        public override float Axis2MouseAcceleration
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.Acceleration : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseAcceleration)
                {
                    mouseAction.Acceleration = value;
                    OnPropertyChanged(nameof(Axis2MouseAcceleration));
                }
            }
        }

        public override bool Axis2MouseFiltering
        {
            get => (Action is MouseActions mouseAction) && mouseAction.Filtering;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseFiltering)
                {
                    mouseAction.Filtering = value;
                    OnPropertyChanged(nameof(Axis2MouseFiltering));
                }
            }
        }

        public override float Axis2MouseFilterCutoff
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.FilterCutoff : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseFilterCutoff)
                {
                    mouseAction.FilterCutoff = value;
                    OnPropertyChanged(nameof(Axis2MouseFilterCutoff));
                }
            }
        }

        public override double Axis2MouseToX
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToX : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseToX)
                {
                    mouseAction.MoveToX = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Axis2MouseToX));
                }
            }
        }

        public override double Axis2MouseToY
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToY : 0;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseToY)
                {
                    mouseAction.MoveToY = value is double.NaN ? 0 : value;
                    OnPropertyChanged(nameof(Axis2MouseToY));
                }
            }
        }

        public override bool Axis2MouseRestore
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.MoveToPrevious : false;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseRestore)
                {
                    mouseAction.MoveToPrevious = value;
                    OnPropertyChanged(nameof(Axis2MouseRestore));
                }
            }
        }

        public override Visibility Axis2MouseTo
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
                    OnPropertyChanged(nameof(Axis2MouseVisibility));
                    OnPropertyChanged(nameof(Axis2ButtonVisibility));
                    OnPropertyChanged(nameof(Axis2MouseTo));
                    OnPropertyChanged(nameof(AxisDirectionVisibility));
                    OnPropertyChanged(nameof(AxisThresholdVisibility));
                    OnPropertyChanged(nameof(GeneralActionVisibility));
                    OnPropertyChanged(nameof(AxisInvertVisibility));
                    break;
            }

            base.OnPropertyChanged(propertyName);
        }

        #endregion

        private AxisStackViewModel _parentStack;
        public AxisStackViewModel ParentStack => _parentStack;

        public ICommand ButtonCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }

        public override Visibility Axis2TouchpadVisibility => Axis2MouseVisibility == Visibility.Visible && TouchpadVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        public override Visibility Axis2JoystickVisibility => Axis2MouseVisibility == Visibility.Visible && JoystickVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

        public override bool IsAxisMapping => true;

        // Axis Direction and Threshold are only visible when converting Axis to Button
        public override Visibility AxisDirectionVisibility => Axis2ButtonVisibility;
        public override Visibility AxisThresholdVisibility => Axis2ButtonVisibility;

        // Axis invert properties should only be visible for Axis -> Joystick mappings
        public override Visibility AxisInvertVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                return currentActionType == ActionType.Joystick ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility TouchpadVisibility => _parentStack._touchpad ? Visibility.Visible : Visibility.Collapsed;
        public Visibility JoystickVisibility => _parentStack._touchpad ? Visibility.Collapsed : Visibility.Visible;

        public AxisMappingViewModel(AxisStackViewModel parentStack, AxisLayoutFlags value) : base(value)
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
                if (Action is not null) Delete();
                SelectedTarget = null;
                OnPropertyChanged(string.Empty);
                return;
            }

            // get current controller
            IController controller = ControllerManager.GetDefault(true);

            // Build Targets
            List<MappingTargetViewModel> targets = new List<MappingTargetViewModel>();

            if (actionType == ActionType.Joystick)
            {
                if (Action is null || Action is not AxisActions)
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
                    var mappingTargetVm = new MappingTargetViewModel
                    {
                        Tag = axis,
                        Content = controller.GetAxisName(axis)
                    };
                    targets.Add(mappingTargetVm);

                    if (axis == ((AxisActions)Action).Axis)
                        matchingTargetVm = mappingTargetVm;
                }

                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Button)
            {
                if (Action is null || Action is not ButtonActions)
                {
                    Action = new ButtonActions()
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

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
                {
                    Action = new KeyboardActions
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                {
                    Action = new MouseActions
                    {
                        motionThreshold = Gamepad.LeftThumbDeadZone,
                        Modifiers = ModifierSet.None,
                        ShiftSlot = ShiftSlot.Any,
                        ShiftMatchAny = false
                    };
                }

                MappingTargetViewModel? matchingTargetVm = null;
                foreach (var mouseType in Enum.GetValues<MouseActionsType>())
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
            else if (actionType == ActionType.Inherit)
            {
                if (Action is null || Action is not InheritActions)
                {
                    Action = new InheritActions();
                }

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

                case ActionType.Joystick:
                    ((AxisActions)Action).Axis = (AxisLayoutFlags)SelectedTarget.Tag;
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
