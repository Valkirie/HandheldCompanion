using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System.Collections.Generic;

namespace HandheldCompanion.ViewModels
{
    public class JoysticksPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<ButtonFlags> _leftThumbButtons =
        [
            ButtonFlags.LeftStickClick, ButtonFlags.LeftStickTouch, ButtonFlags.LeftStickUp, ButtonFlags.LeftStickDown,
            ButtonFlags.LeftStickLeft, ButtonFlags.LeftStickRight
        ];

        private static readonly List<AxisLayoutFlags> _leftThumbAxis = [AxisLayoutFlags.LeftStick];

        private static readonly List<ButtonFlags> _rightThumbButtons =
        [
            ButtonFlags.RightStickClick, ButtonFlags.RightStickTouch, ButtonFlags.RightStickUp, ButtonFlags.RightStickDown,
            ButtonFlags.RightStickLeft, ButtonFlags.RightStickRight
        ];

        private static readonly List<AxisLayoutFlags> _rightThumbAxis = [AxisLayoutFlags.RightStick];

        public List<ButtonStackViewModel> LeftThumbMappings { get; private set; } = [];
        public List<AxisMappingViewModel> LeftThumbAxisMappings { get; private set; } = [];
        public List<ButtonStackViewModel> RightThumbMappings { get; private set; } = [];
        public List<AxisMappingViewModel> RightThumbAxisMappings { get; private set; } = [];

        private bool _isLeftThumbEnabled;
        public bool IsLeftThumbEnabled
        {
            get => _isLeftThumbEnabled;
            set
            {
                if (value != IsLeftThumbEnabled)
                {
                    _isLeftThumbEnabled = value;
                    OnPropertyChanged(nameof(IsLeftThumbEnabled));
                }
            }
        }

        private bool _isRightThumbEnabled;
        public bool IsRightThumbEnabled
        {
            get => _isRightThumbEnabled;
            set
            {
                if (value != IsRightThumbEnabled)
                {
                    _isRightThumbEnabled = value;
                    OnPropertyChanged(nameof(IsRightThumbEnabled));
                }
            }
        }

        public JoysticksPageViewModel()
        {
            foreach (var flag in _leftThumbButtons)
            {
                LeftThumbMappings.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _leftThumbAxis)
            {
                LeftThumbAxisMappings.Add(new AxisMappingViewModel(flag));
            }

            foreach (var flag in _rightThumbButtons)
            {
                RightThumbMappings.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _rightThumbAxis)
            {
                RightThumbAxisMappings.Add(new AxisMappingViewModel(flag));
            }
        }

        protected override void UpdateController(IController controller)
        {
            IsLeftThumbEnabled = controller.HasSourceAxis(_leftThumbAxis);
            IsRightThumbEnabled = controller.HasSourceAxis(_rightThumbAxis);

            IsEnabled = IsLeftThumbEnabled || IsRightThumbEnabled;
        }
    }
}
