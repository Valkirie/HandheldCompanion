using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Views;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.ViewModels
{
    public class ButtonsPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<ButtonFlags> _ABXY = [ButtonFlags.B1, ButtonFlags.B2, ButtonFlags.B3, ButtonFlags.B4, ButtonFlags.B5, ButtonFlags.B6, ButtonFlags.B7, ButtonFlags.B8];
        private static readonly List<ButtonFlags> _BUMPERS = [ButtonFlags.L1, ButtonFlags.R1];
        private static readonly List<ButtonFlags> _MENU = [ButtonFlags.Back, ButtonFlags.Start, ButtonFlags.Special, ButtonFlags.Special2];
        private static readonly List<ButtonFlags> _BACKGRIPS = [ButtonFlags.L4, ButtonFlags.L5, ButtonFlags.R4, ButtonFlags.R5];
        
        private bool _isABXYEnabled;
        public bool IsABXYEnabled
        {
            get => _isABXYEnabled;
            set
            {
                if (value != IsABXYEnabled)
                {
                    _isABXYEnabled = value;
                    OnPropertyChanged(nameof(IsABXYEnabled));
                }
            }
        }

        private bool _isBUMPERSEnabled;
        public bool IsBUMPERSEnabled
        {
            get => _isBUMPERSEnabled;
            set
            {
                if (value != _isBUMPERSEnabled)
                {
                    _isBUMPERSEnabled = value;
                    OnPropertyChanged(nameof(IsBUMPERSEnabled));
                }
            }
        }

        private bool _isMENUEnabled;
        public bool IsMENUEnabled
        {
            get => _isMENUEnabled;
            set
            {
                if (value != _isMENUEnabled)
                {
                    _isMENUEnabled = value;
                    OnPropertyChanged(nameof(IsMENUEnabled));
                }
            }
        }

        private bool _isBACKGRIPSEnabled;
        public bool IsBACKGRIPSEnabled
        {
            get => _isBACKGRIPSEnabled;
            set
            {
                if (value != _isBACKGRIPSEnabled)
                {
                    _isBACKGRIPSEnabled = value;
                    OnPropertyChanged(nameof(IsBACKGRIPSEnabled));
                }
            }
        }

        public bool IsOEMEnabled => IDevice.GetCurrent().OEMButtons.Any();

        public List<ButtonStackViewModel> ABXYStacks { get; private set; } = [];
        public List<ButtonStackViewModel> BUMPERSStacks { get; private set; } = [];
        public List<ButtonStackViewModel> MENUStacks { get; private set; } = [];
        public List<ButtonStackViewModel> BACKGRIPSStacks { get; private set; } = [];
        public List<ButtonStackViewModel> OEMStacks { get; private set; } = [];

        public ButtonsPageViewModel()
        {
            foreach (var flag in _ABXY)
            {
                ABXYStacks.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _BUMPERS)
            {
                BUMPERSStacks.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _MENU)
            {
                MENUStacks.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in _BACKGRIPS)
            {
                BACKGRIPSStacks.Add(new ButtonStackViewModel(flag));
            }

            foreach (var flag in IDevice.GetCurrent().OEMButtons)
            {
                OEMStacks.Add(new ButtonStackViewModel(flag));
            }
        }

        protected override void UpdateController(IController controller)
        {
            IsABXYEnabled = controller.HasSourceButton(_ABXY);
            IsBUMPERSEnabled = controller.HasSourceButton(_BUMPERS);
            IsMENUEnabled = controller.HasSourceButton(_MENU);
            IsBACKGRIPSEnabled = controller.HasSourceButton(_BACKGRIPS);

            IsEnabled = IsABXYEnabled || IsBUMPERSEnabled || IsMENUEnabled || IsBACKGRIPSEnabled;
        }
    }
}
