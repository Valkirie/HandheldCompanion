using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Linq;

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

        public TriggerMappingViewModel(AxisLayoutFlags value) : base(value)
        {
        }

        protected override void UpdateController(IController controller)
        {
            var flag = (AxisLayoutFlags)Value;

            IsSupported = controller.HasSourceAxis(flag);

            if (IsSupported)
            {
                UpdateIcon(controller.GetGlyphIconInfo(flag));
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
                var controller = ControllerManager.GetEmulatedController();

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
            if (layout.AxisLayout.TryGetValue((AxisLayoutFlags)Value, out var newAction))
                SetAction(newAction, false);
            else
                Reset();
        }
    }
}
