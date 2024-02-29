using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using System.Collections.Generic;

namespace HandheldCompanion.ViewModels
{
    public class DpadPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<ButtonFlags> _buttonFlags = [ButtonFlags.DPadUp, ButtonFlags.DPadDown, ButtonFlags.DPadLeft, ButtonFlags.DPadRight];

        public List<ButtonStackViewModel> ButtonStacks { get; private set; } = [];
        public DpadPageViewModel()
        {
            foreach (var flag in _buttonFlags)
            {
                ButtonStacks.Add(new ButtonStackViewModel(flag));
            }
        }

        protected override void UpdateController(IController controller)
        {
            IsEnabled = controller.HasSourceButton(_buttonFlags);
        }
    }
}
