using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System.Collections.Generic;

namespace HandheldCompanion.ViewModels
{
    public class TrackpadsPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<ButtonFlags> _leftButtons =
        [
            ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClick, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown,
            ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight
        ];

        private static readonly List<AxisLayoutFlags> _leftAxis = [AxisLayoutFlags.LeftPad];

        private static readonly List<ButtonFlags> _rightButtons =
        [
            ButtonFlags.RightPadTouch, ButtonFlags.RightPadClick, ButtonFlags.RightPadClickUp,
            ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight
        ];

        private static readonly List<AxisLayoutFlags> _rightAxis = [AxisLayoutFlags.RightPad];

        public List<ButtonStackViewModel> LeftButtonsMappings { get; private set; } = [];
        public List<AxisMappingViewModel> LeftAxisMappings { get; private set; } = [];
        public List<ButtonStackViewModel> RightButtonsMappings { get; private set; } = [];
        public List<AxisMappingViewModel> RightAxisMappings { get; private set; } = [];

        private bool _isLeftPadEnabled;
        public bool IsLeftPadEnabled
        {
            get => _isLeftPadEnabled;
            set
            {
                if (value != IsLeftPadEnabled)
                {
                    _isLeftPadEnabled = value;
                    OnPropertyChanged(nameof(IsLeftPadEnabled));
                }
            }
        }

        private bool _isRightPadEnabled;
        public bool IsRightPadEnabled
        {
            get => _isRightPadEnabled;
            set
            {
                if (value != IsRightPadEnabled)
                {
                    _isRightPadEnabled = value;
                    OnPropertyChanged(nameof(IsRightPadEnabled));
                }
            }
        }

        public TrackpadsPageViewModel()
        {
            foreach (var flag in _leftButtons)
            {
                LeftButtonsMappings.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _leftAxis)
            {
                LeftAxisMappings.Add(new AxisMappingViewModel(flag));
            }

            foreach (var flag in _rightButtons)
            {
                RightButtonsMappings.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _rightAxis)
            {
                RightAxisMappings.Add(new AxisMappingViewModel(flag));
            }
        }

        protected override void UpdateController(IController controller)
        {
            IsLeftPadEnabled = controller.HasSourceAxis(_leftAxis);
            IsRightPadEnabled = controller.HasSourceAxis(_rightAxis);

            IsEnabled = IsLeftPadEnabled || IsRightPadEnabled;
        }
    }
}
