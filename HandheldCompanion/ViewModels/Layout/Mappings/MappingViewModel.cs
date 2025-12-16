using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace HandheldCompanion.ViewModels
{
    // ViewModel used to fill Target ComboBox on Mappings
    public class MappingTargetViewModel : BaseViewModel
    {
        public object Tag { get; set; }
        public string Content { get; set; }

        public override string ToString()
        {
            return Content;
        }
    }

    public abstract class MappingViewModel : BaseViewModel
    {
        protected object Value { get; set; }
        public IActions? Action { get; protected set; }

        public int ActionTypeIndex
        {
            get => Action is not null ? (int)Action.actionType : 0;
            set
            {
                if (value != ActionTypeIndex)
                {
                    if (Action is not null)
                        Action.actionType = (ActionType)value;

                    ActionTypeChanged((ActionType)value);

                    // dirty hack to show/hide StackPanel based on ActionType and SelectedTarget
                    OnPropertyChanged(nameof(Axis2MouseVisibility));
                    OnPropertyChanged(nameof(Axis2ButtonVisibility));
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

        public float StartDelay
        {
            get => Action is not null ? Action.StartDelay : 0;
            set
            {
                if (Action is not null && value != StartDelay)
                {
                    Action.StartDelay = value;
                    OnPropertyChanged(nameof(StartDelay));
                }
            }
        }

        public ObservableCollection<MappingTargetViewModel> Targets { get; set; } = [];

        private MappingTargetViewModel? _selectedTarget;
        public MappingTargetViewModel? SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                if (value != SelectedTarget)
                {
                    _selectedTarget = value;
                    TargetTypeChanged();
                    OnPropertyChanged(nameof(SelectedTarget));

                    // dirty hack to show/hide StackPanel based on ActionType and SelectedTarget
                    OnPropertyChanged(nameof(Axis2MouseVisibility));
                    OnPropertyChanged(nameof(Axis2ButtonVisibility));
                }
            }
        }

        public int Axis2ButtonThreshold
        {
            get => (int)((Action is IActions iActions) ? iActions.motionThreshold : 0);
            set
            {
                if (Action is IActions iActions && value != Axis2ButtonThreshold)
                {
                    iActions.motionThreshold = value;
                    OnPropertyChanged(nameof(Axis2ButtonThreshold));
                }
            }
        }

        private bool _isSupported;
        public bool IsSupported
        {
            get => _isSupported;
            set
            {
                if (value != _isSupported)
                {
                    _isSupported = value;
                    OnPropertyChanged(nameof(IsSupported));
                }
            }
        }

        // Shows the Axis2 panel for Mouse actions of type Scroll or Move
        public Visibility Axis2MouseVisibility
        {
            get
            {
                if (SelectedTarget == null)
                    return Visibility.Collapsed;

                // Check if the current action is a Mouse action
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType != ActionType.Mouse)
                    return Visibility.Collapsed;

                // Only show if the mouse action is Scroll or Move
                MouseActionsType mouseAction = (MouseActionsType)SelectedTarget.Tag;
                return (mouseAction == MouseActionsType.Scroll || mouseAction == MouseActionsType.Move)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        // Shows the Axis2 panel for Button, Keyboard,
        // or for Mouse actions that are NOT Scroll or Move.
        public Visibility Axis2ButtonVisibility
        {
            get
            {
                ActionType currentActionType = (ActionType)ActionTypeIndex;
                if (currentActionType == ActionType.Button || currentActionType == ActionType.Keyboard)
                    return Visibility.Visible;

                // For Mouse actions, ensure a target exists and check its action type
                if (currentActionType == ActionType.Mouse && SelectedTarget != null)
                {
                    MouseActionsType mouseAction = (MouseActionsType)SelectedTarget.Tag;
                    if (mouseAction != MouseActionsType.Scroll && mouseAction != MouseActionsType.Move)
                        return Visibility.Visible;
                }

                return Visibility.Collapsed;
            }
        }

        // Purely UI related properties, they should NOT update the Layout
        // Avoid unnecessary save/update calls
        protected HashSet<string> ExcludedUpdateProperties =
        [
            nameof(IsSupported),
        ];

        // Property to block off updating to model in certain cases
        protected bool _updateToModel = true;
        protected static List<MappingTargetViewModel> _keyboardKeysTargets = [];

        public MappingViewModel(object value)
        {
            Value = value;

            // Enable thread-safe access to the collection
            BindingOperations.EnableCollectionSynchronization(Targets, new object());

            // manage events
            MainWindow.layoutPage.LayoutUpdated += UpdateMapping;
            VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;

            // Lazy initialize to avoid re-creating target for Keyboard targets
            if (_keyboardKeysTargets.Count == 0)
            {
                foreach (KeyFlags key in KeyFlagsOrder.arr)
                {
                    _keyboardKeysTargets.Add(new MappingTargetViewModel
                    {
                        Tag = (VirtualKeyCode)key,
                        Content = EnumUtils.GetDescriptionFromEnumValue(key)
                    });
                }
            }

            // Send update event to Model
            PropertyChanged +=
                (s, e) =>
                {
                    if (_updateToModel && e.PropertyName is not null && !ExcludedUpdateProperties.Contains(e.PropertyName))
                        Update();
                };
        }

        public override void Dispose()
        {
            MainWindow.layoutPage.LayoutUpdated -= UpdateMapping;
            VirtualManager.ControllerSelected -= VirtualManager_ControllerSelected;

            base.Dispose();
        }

        private void VirtualManager_ControllerSelected(HIDmode hid) => ActionTypeChanged();

        protected abstract void ActionTypeChanged(ActionType? newActionType = null);
        protected abstract void TargetTypeChanged();
        protected abstract void Update();
        protected abstract void Delete();
        protected abstract void UpdateMapping(Layout layout);

        public virtual void SetAction(IActions newAction, bool updateToModel = true)
        {
            _selectedTarget = null;
            Action = newAction;

            _updateToModel = updateToModel;

            ActionTypeChanged(); // Includes full UI update

            // Reset update to model
            _updateToModel = true;
        }

        public virtual void Reset()
        {
            ActionTypeIndex = 0;
            SelectedTarget = null;
        }
    }
}
