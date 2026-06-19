namespace HandheldCompanion.ViewModels
{
    public class LayoutItemPageViewModel : BaseViewModel
    {
        private MappingViewModel? _currentMapping;
        private string _actionTitle = "Action Settings";
        private string _actionType = "Action Type";
        private string _actionDescription = "Configure action settings";

        public MappingViewModel? CurrentMapping
        {
            get => _currentMapping;
            set
            {
                if (value != _currentMapping)
                {
                    // Unsubscribe from old mapping
                    _currentMapping?.PropertyChanged -= CurrentMapping_PropertyChanged;

                    _currentMapping = value;

                    // Subscribe to new mapping
                    _currentMapping?.PropertyChanged += CurrentMapping_PropertyChanged;

                    // Update the display strings
                    UpdateDisplay();
                }
            }
        }

        public string ActionTitle
        {
            get => _actionTitle;
            private set
            {
                if (value != _actionTitle)
                {
                    _actionTitle = value;
                    OnPropertyChanged(nameof(ActionTitle));
                }
            }
        }

        public string ActionType
        {
            get => _actionType;
            private set
            {
                if (value != _actionType)
                {
                    _actionType = value;
                    OnPropertyChanged(nameof(ActionType));
                }
            }
        }

        public string ActionDescription
        {
            get => _actionDescription;
            private set
            {
                if (value != _actionDescription)
                {
                    _actionDescription = value;
                    OnPropertyChanged(nameof(ActionDescription));
                }
            }
        }

        private void CurrentMapping_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Recompute display when ActionTypeIndex or SelectedTarget changes
            if (e.PropertyName == nameof(MappingViewModel.ActionTypeIndex) ||
                e.PropertyName == nameof(MappingViewModel.SelectedTarget))
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (_currentMapping is null)
            {
                ActionTitle = "Action Settings";
                ActionDescription = "Configure action settings";
                return;
            }

            // Update title with input name from parent stack
            string inputName = "Unknown Input";
            string inputType = "Unknown Type";
            if (_currentMapping is ButtonMappingViewModel buttonMapping)
            {
                inputName = buttonMapping.ParentStack?.Name ?? "Unknown Button";
                inputType = "Button";
            }
            else if (_currentMapping is TriggerMappingViewModel triggerMapping)
            {
                inputName = triggerMapping.ParentStack?.Name ?? "Unknown Trigger";
                inputType = "Trigger";
            }
            else if (_currentMapping is AxisMappingViewModel axisMapping)
            {
                inputName = axisMapping.ParentStack?.Name ?? "Unknown Axis";
                inputType = "Axis";
            }

            ActionTitle = inputName;
            ActionType = inputType;

            // Update description based on action and target
            if (_currentMapping.Action is not null)
            {
                string actionType = _currentMapping.ActionTypeIndex switch
                {
                    0 => "Disabled",
                    1 => "Button",
                    2 => "Joystick",
                    3 => "Keyboard",
                    4 => "Mouse",
                    5 => "Trigger",
                    6 => "Shift",
                    7 => "Inherit",
                    _ => "Unknown"
                };

                ActionDescription = $"{actionType}";
                if (_currentMapping.SelectedTarget is not null)
                    ActionDescription = $"{actionType}: {_currentMapping.SelectedTarget.Content}";
            }
            else
            {
                ActionDescription = "Configure action settings";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from current mapping
                _currentMapping?.PropertyChanged -= CurrentMapping_PropertyChanged;
            }

            base.Dispose(disposing);
        }
    }
}
