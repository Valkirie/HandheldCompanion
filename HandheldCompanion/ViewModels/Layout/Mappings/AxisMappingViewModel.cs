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
using System.Windows;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class AxisMappingViewModel : MappingViewModel
    {
        #region Axis Action Properties

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

        #region Axis2Button
        public int Axis2ButtonDirection
        {
            get => (int)((Action is IActions iActions) ? iActions.motionDirection : 0);
            set
            {
                if (Action is IActions iActions && value != Axis2ButtonDirection)
                {
                    iActions.motionDirection = (MotionDirection)value;
                    OnPropertyChanged(nameof(Axis2ButtonDirection));

                    // Cascade notifications to dependent properties
                    OnPropertyChanged(nameof(IsLeft));
                    OnPropertyChanged(nameof(IsRight));
                    OnPropertyChanged(nameof(IsUp));
                    OnPropertyChanged(nameof(IsDown));
                }
            }
        }

        public bool IsLeft
        {
            get => ((MotionDirection)Axis2ButtonDirection).HasFlag(MotionDirection.Left);
            set
            {
                if (value != IsLeft)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)MotionDirection.Left
                        : Axis2ButtonDirection & ~(int)MotionDirection.Left;
                }
            }
        }

        public bool IsRight
        {
            get => ((MotionDirection)Axis2ButtonDirection).HasFlag(MotionDirection.Right);
            set
            {
                if (value != IsRight)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)MotionDirection.Right
                        : Axis2ButtonDirection & ~(int)MotionDirection.Right;
                }
            }
        }

        public bool IsUp
        {
            get => ((MotionDirection)Axis2ButtonDirection).HasFlag(MotionDirection.Up);
            set
            {
                if (value != IsUp)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)MotionDirection.Up
                        : Axis2ButtonDirection & ~(int)MotionDirection.Up;
                }
            }
        }

        public bool IsDown
        {
            get => ((MotionDirection)Axis2ButtonDirection).HasFlag(MotionDirection.Down);
            set
            {
                if (value != IsDown)
                {
                    Axis2ButtonDirection = value
                        ? Axis2ButtonDirection | (int)MotionDirection.Down
                        : Axis2ButtonDirection & ~(int)MotionDirection.Down;
                }
            }
        }
        #endregion

        public int Axis2AxisInnerDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneInner : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisInnerDeadzone)
                {
                    axisAction.AxisDeadZoneInner = value;
                    OnPropertyChanged(nameof(Axis2AxisInnerDeadzone));
                }
            }
        }

        public int Axis2AxisOuterDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisDeadZoneOuter : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisOuterDeadzone)
                {
                    axisAction.AxisDeadZoneOuter = value;
                    OnPropertyChanged(nameof(Axis2AxisOuterDeadzone));
                }
            }
        }

        public int Axis2AxisAntiDeadzone
        {
            get => (Action is AxisActions axisAction) ? axisAction.AxisAntiDeadZone : 0;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisAntiDeadzone)
                {
                    axisAction.AxisAntiDeadZone = value;
                    OnPropertyChanged(nameof(Axis2AxisAntiDeadzone));
                }
            }
        }

        public int Axis2AxisOutputShapeIndex
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

        public bool Axis2AxisInvertHorizontal
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

        public bool Axis2AxisInvertVertical
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

        public int Axis2MousePointerSpeed
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

        public int Axis2MouseDeadzone
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

        public float Axis2MouseAcceleration
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

        public bool Axis2MouseFiltering
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

        public float Axis2MouseFilterCutoff
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

        #endregion

        private AxisStackViewModel _parentStack;

        public ICommand ButtonCommand { get; private set; }

        public Visibility Axis2TouchpadVisibility => Axis2MouseVisibility == Visibility.Visible && TouchpadVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Axis2JoystickVisibility => Axis2MouseVisibility == Visibility.Visible && JoystickVisibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;

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
                    Action = new AxisActions();

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
                    Action = new ButtonActions() { motionThreshold = Gamepad.LeftThumbDeadZone };

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
                    Action = new KeyboardActions { motionThreshold = Gamepad.LeftThumbDeadZone };

                Targets.ReplaceWith(_keyboardKeysTargets);
                SelectedTarget = _keyboardKeysTargets.FirstOrDefault(e => e.Tag.Equals(((KeyboardActions)Action).Key)) ?? _keyboardKeysTargets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                    Action = new MouseActions { motionThreshold = Gamepad.LeftThumbDeadZone };

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
