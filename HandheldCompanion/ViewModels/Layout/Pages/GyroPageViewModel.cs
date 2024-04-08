using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using System.Collections.Generic;

namespace HandheldCompanion.ViewModels
{
    public class GyroPageViewModel : ILayoutPageViewModel
    {
        private static readonly List<AxisLayoutFlags> _pageLayoutFlags = [AxisLayoutFlags.Gyroscope];

        public List<GyroMappingViewModel> GyroMappings { get; private set; } = [];

        public GyroPageViewModel()
        {
            foreach (var layoutFlag in _pageLayoutFlags)
            {
                GyroMappings.Add(new GyroMappingViewModel(layoutFlag));
            }
        }

        protected override void UpdateController(IController controller)
        {
            IsEnabled = controller.HasSourceAxis(_pageLayoutFlags) || IDevice.GetCurrent().HasMotionSensor();
        }
    }
}
