using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class TriggerMappingViewModel : MappingViewModel
    {
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

        private TriggerStackViewModel _parentStack;

        public bool IsInitialMapping { get; set; } = false;

        public ICommand ButtonCommand { get; private set; }

        public TriggerMappingViewModel(TriggerStackViewModel parentStack, AxisLayoutFlags value, bool isInitialMapping = false) : base(value)
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

            if (actionType == ActionType.Trigger)
            {
                if (Action is null || Action is not TriggerActions)
                {
                    Action = new TriggerActions();
                }

                // get current controller
                var controller = ControllerManager.GetDefault();

                // Build Targets
                var targets = new List<MappingTargetViewModel>();

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
                    {
                        matchingTargetVm = mappingTargetVm;
                    }
                }

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

        // Done from AxisStack
        protected override void UpdateMapping(Layout layout)
        {
        }
    }
}
