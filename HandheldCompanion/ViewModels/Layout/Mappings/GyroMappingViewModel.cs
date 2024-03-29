using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class GyroMappingViewModel : MappingViewModel
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

        public bool Axis2AxisAutoRotate
        {
            get => (Action is AxisActions axisAction) ? axisAction.AutoRotate : false;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisAutoRotate)
                {
                    axisAction.AutoRotate = value;
                    OnPropertyChanged(nameof(Axis2AxisAutoRotate));
                }
            }
        }

        public int Axis2AxisRotation
        {
            get
            {
                if (Action is AxisActions axisAction)
                {
                    return (axisAction.AxisInverted ? 180 : 0) + (axisAction.AxisRotated ? 90 : 0);
                }

                return 0;
            }
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisRotation)
                {
                    axisAction.AxisInverted = ((value / 90) & 2) == 2;
                    axisAction.AxisRotated = ((value / 90) & 1) == 1;
                    OnPropertyChanged(nameof(Axis2AxisRotation));
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

        public bool Axis2AxisImproveCircularity
        {
            get => (Action is AxisActions axisAction) ? axisAction.ImproveCircularity : false;
            set
            {
                if (Action is AxisActions axisAction && value != Axis2AxisImproveCircularity)
                {
                    axisAction.ImproveCircularity = value;
                    OnPropertyChanged(nameof(Axis2AxisImproveCircularity));
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

        public bool Axis2MouseAutoRotate
        {
            get => (Action is MouseActions mouseAction) ? mouseAction.AutoRotate : false;
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseAutoRotate)
                {
                    mouseAction.AutoRotate = value;
                    OnPropertyChanged(nameof(Axis2MouseAutoRotate));
                }
            }
        }

        public int Axis2MouseRotation
        {
            get
            {
                if (Action is MouseActions mouseAction)
                {
                    return (mouseAction.AxisInverted ? 180 : 0) + (mouseAction.AxisRotated ? 90 : 0);
                }

                return 0;
            }
            set
            {
                if (Action is MouseActions mouseAction && value != Axis2MouseRotation)
                {
                    mouseAction.AxisInverted = ((value / 90) & 2) == 2;
                    mouseAction.AxisRotated = ((value / 90) & 1) == 1;
                    OnPropertyChanged(nameof(Axis2MouseRotation));
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

        #endregion

        public int MotionInputIndex
        {
            get => (Action is GyroActions gyroAction) ? (int)gyroAction.MotionInput : -1;
            set
            {
                if (Action is GyroActions gyroAction && value != MotionInputIndex)
                {
                    gyroAction.MotionInput = (MotionInput)value;
                    OnPropertyChanged(nameof(MotionInputIndex));
                }
            }
        }

        public int MotionModeIndex
        {
            get => (Action is GyroActions gyroAction) ? (int)gyroAction.MotionMode : -1;
            set
            {
                if (Action is GyroActions gyroAction && value != MotionModeIndex)
                {
                    gyroAction.MotionMode = (MotionMode) value;
                    OnPropertyChanged(nameof(MotionModeIndex));
                }
            }
        }

        public float GyroWeight
        {
            get => (Action is GyroActions gyroAction) ? gyroAction.gyroWeight : 0;
            set
            {
                if (Action is GyroActions gyroAction && value != GyroWeight)
                {
                    gyroAction.gyroWeight = value;
                    OnPropertyChanged(nameof(GyroWeight));
                }
            }
        }

        private Hotkey _gyroHotkey;
        public object? HotkeyContent => _gyroHotkey?.GetControl();

        public List<MotionInputViewModel> MotionInputItems { get; private set; } = [];

        public GyroMappingViewModel(AxisLayoutFlags layoutFlag) : base(layoutFlag)
        {
            foreach (var mode in Enum.GetValues<MotionInput>())
            {
                MotionInputItems.Add(new MotionInputViewModel
                {
                    Glyph = mode.ToGlyph(),
                    Description = EnumUtils.GetDescriptionFromEnumValue(mode)
                });
            }
            _gyroHotkey = HotkeysManager.Hotkeys.Values.First(item => item.inputsHotkey.Listener.Equals("shortcutProfilesPage@"));

            InputsManager.TriggerUpdated += InputsManager_TriggerUpdated;
        }

        public override void Dispose()
        {
            InputsManager.TriggerUpdated -= InputsManager_TriggerUpdated;
            base.Dispose();
        }

        private void InputsManager_TriggerUpdated(string listener, InputsChord inputs, InputsManager.ListenerType type)
        {
            if (Action is not GyroActions gyroAction)
                return;
            
            switch (listener)
            {
                case "shortcutProfilesPage@":
                    {
                        gyroAction.MotionTrigger = inputs.State.Clone() as ButtonState;

                        // update hotkey UI
                        _gyroHotkey.inputsChord.State = gyroAction.MotionTrigger.Clone() as ButtonState;
                        _gyroHotkey.DrawInput();
                    }                  
                    break;
            }

            Update();
        }

        protected override void UpdateController(IController controller)
        {
            var flag = (AxisLayoutFlags)Value;

            IsSupported = controller.HasSourceAxis(flag) || IDevice.GetCurrent().HasMotionSensor();

            if (IsSupported)
            {
                UpdateIcon(controller.GetGlyphIconInfo(flag));
            }
        }

        protected override void ActionTypeChanged(ActionType? newActionType = null)
        {
            var actionType = newActionType ?? (ActionType) ActionTypeIndex;
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
                    Action = new AxisActions()
                    {
                        Axis = GyroActions.DefaultAxisLayoutFlags,
                        AxisAntiDeadZone = GyroActions.DefaultAxisAntiDeadZone,
                        MotionTrigger = _gyroHotkey.inputsChord.State.Clone() as ButtonState
                    };
                }

                // get current controller
                var controller = ControllerManager.GetEmulatedController();

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
                    Action = new MouseActions()
                    {
                        MouseType = GyroActions.DefaultMouseActionsType,
                        Sensivity = GyroActions.DefaultSensivity,
                        Deadzone = GyroActions.DefaultDeadzone,
                        MotionTrigger = _gyroHotkey.inputsChord.State.Clone() as ButtonState
                    };
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
            if (Action is null) return;
            MainWindow.layoutPage.CurrentLayout.UpdateLayout((AxisLayoutFlags)Value, Action);
        }

        protected override void Delete()
        {
            Action = null;
            MainWindow.layoutPage.CurrentLayout.RemoveLayout((AxisLayoutFlags)Value);
        }

        protected override void UpdateMapping(Layout layout)
        {
            if (layout.GyroLayout.TryGetValue((AxisLayoutFlags) Value, out var newAction))
            {
                _gyroHotkey.inputsChord.State = ((GyroActions)newAction).MotionTrigger.Clone() as ButtonState;
                _gyroHotkey.DrawInput();
                SetAction(newAction, false);
            }
            else
            {
                Reset();
            }
        }
    }
}
