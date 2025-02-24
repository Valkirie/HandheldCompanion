using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class AxisMappingViewModel : MappingViewModel
    {
        private static readonly HashSet<MouseActionsType> _unsupportedMouseActionTypes =
        [
            MouseActionsType.LeftButton,
            MouseActionsType.RightButton,
            MouseActionsType.MiddleButton,
            MouseActionsType.ScrollUp,
            MouseActionsType.ScrollDown
        ];

        #region Axis Action Properties

        // default mapping can't be shifted
        public int ShiftIndex
        {
            get => IsInitialMapping ? 0 : Action is not null ? (int)Action.ShiftSlot : 0;
            set
            {
                if (IsInitialMapping)
                    return;

                if (Action is not null && value != ShiftIndex)
                {
                    Action.ShiftSlot = (ShiftSlot)value;
                    OnPropertyChanged(nameof(ShiftIndex));
                }
            }
        }

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

        public bool IsInitialMapping { get; set; } = false;

        public ICommand ButtonCommand { get; private set; }

        public AxisMappingViewModel(AxisStackViewModel parentStack, AxisLayoutFlags value, bool isInitialMapping = false) : base(value)
        {
            _parentStack = parentStack;
            IsInitialMapping = isInitialMapping;

            ButtonCommand = new DelegateCommand(() =>
            {
                if (IsInitialMapping)
                    _parentStack.AddMapping();
                else
                {
                    if (Action is not null) Delete();
                    _parentStack.RemoveMapping(this);
                }
            });

            if (isInitialMapping)
            {
                var controller = ControllerManager.GetTarget();
                if (controller is not null) UpdateController(controller);
            }
        }

        protected override void UpdateController(IController controller)
        {
            var flag = (AxisLayoutFlags)Value;

            IsSupported = controller.HasSourceAxis(flag);

            if (IsSupported)
            {
                UpdateIcon(controller.GetGlyphIconInfo(flag, 28));
            }
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

            if (actionType == ActionType.Joystick)
            {
                if (Action is null || Action is not AxisActions)
                {
                    Action = new AxisActions();
                }

                // get current controller
                var controller = ControllerManager.GetDefault(true);

                // Build Targets
                var targets = new List<MappingTargetViewModel>();

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
                    {
                        matchingTargetVm = mappingTargetVm;
                    }
                }

                Targets.ReplaceWith(targets);
                SelectedTarget = matchingTargetVm ?? Targets.First();
            }
            else if (actionType == ActionType.Mouse)
            {
                if (Action is null || Action is not MouseActions)
                {
                    Action = new MouseActions();
                }

                // Build Targets
                var targets = new List<MappingTargetViewModel>();

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
                    {
                        matchingTargetVm = mappingTargetVm;
                    }
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
