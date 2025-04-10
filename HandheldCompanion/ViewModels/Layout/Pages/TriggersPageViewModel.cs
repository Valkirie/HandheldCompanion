﻿using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System.Collections.Generic;

namespace HandheldCompanion.ViewModels
{
    public class TriggersPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<AxisLayoutFlags> _leftTriggerAxis = [AxisLayoutFlags.L2];
        private static readonly List<AxisLayoutFlags> _rightTriggerAxis = [AxisLayoutFlags.R2];

        public List<TriggerStackViewModel> LeftTriggerAxisMappings { get; private set; } = [];
        public List<TriggerStackViewModel> RightTriggerAxisMappings { get; private set; } = [];

        private bool _isLeftTriggerEnabled;
        public bool IsLeftTriggerEnabled
        {
            get => _isLeftTriggerEnabled;
            set
            {
                if (value != IsLeftTriggerEnabled)
                {
                    _isLeftTriggerEnabled = value;
                    OnPropertyChanged(nameof(IsLeftTriggerEnabled));
                }
            }
        }

        private bool _isRightTriggerEnabled;
        public bool IsRightTriggerEnabled
        {
            get => _isRightTriggerEnabled;
            set
            {
                if (value != IsRightTriggerEnabled)
                {
                    _isRightTriggerEnabled = value;
                    OnPropertyChanged(nameof(IsRightTriggerEnabled));
                }
            }
        }

        public TriggersPageViewModel()
        {
            foreach (var flag in _leftTriggerAxis)
                LeftTriggerAxisMappings.Add(new TriggerStackViewModel(flag));

            foreach (var flag in _rightTriggerAxis)
                RightTriggerAxisMappings.Add(new TriggerStackViewModel(flag));
        }

        protected override void UpdateController(IController controller)
        {
            IsLeftTriggerEnabled = controller.HasSourceAxis(_leftTriggerAxis);
            IsRightTriggerEnabled = controller.HasSourceAxis(_rightTriggerAxis);

            IsEnabled = IsLeftTriggerEnabled || IsRightTriggerEnabled;
        }
    }
}
