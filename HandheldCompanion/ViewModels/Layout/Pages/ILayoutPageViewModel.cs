using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;

namespace HandheldCompanion.ViewModels
{
    public abstract class ILayoutPageViewModel : BaseViewModel
    {
        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (value != _isEnabled)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        public ILayoutPageViewModel()
        {
            // manage events
            ControllerManager.ControllerSelected += UpdateController;

            // raise events
            if (ControllerManager.GetTarget() is IController controller)
                UpdateController(controller);
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ControllerManager.ControllerSelected -= UpdateController;
            }

            base.Dispose(disposing);
        }

        protected abstract void UpdateController(IController controller);
    }
}
