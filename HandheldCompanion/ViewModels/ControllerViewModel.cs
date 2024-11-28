using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using System.Windows.Input;

namespace HandheldCompanion.ViewModels
{
    public class ControllerViewModel : BaseViewModel
    {
        private IController _controller;
        public IController Controller
        {
            get => _controller;
            set
            {
                _controller = value;
                Updated();
            }
        }

        public string Name => _controller is not null ? _controller.ToString() : "N/A";
        public int UserIndex => _controller is not null ? _controller.GetUserIndex() : 0;

        public bool CanCalibrate => _controller is not null && _controller.HasMotionSensor();
        public string Enumerator => _controller is not null ? _controller.Details.EnumeratorName : "USB";

        public bool IsBusy => _controller is not null && _controller.IsBusy;
        public bool IsVirtual => _controller is not null && _controller.IsVirtual();
        public bool IsPlugged => _controller is not null && ControllerManager.GetTargetController().GetInstancePath() == _controller.GetInstancePath();
        public bool IsHidden => _controller is not null && _controller.IsHidden();

        public bool IsInternal => _controller is not null && _controller.Details.isInternal;
        public bool IsWireless => _controller is not null && _controller.IsWireless;
        public bool IsDongle => _controller is not null && _controller.IsDongle;

        public ICommand ConnectCommand { get; private set; }
        public ICommand HideCommand { get; private set; }
        public ICommand CalibrateCommand { get; private set; }

        public ControllerViewModel() { }

        public ControllerViewModel(IController controller)
        {
            Controller = controller;
            Controller.UserIndexChanged += Controller_UserIndexChanged;
            Controller.StateChanged += Controller_StateChanged;

            ConnectCommand = new DelegateCommand(async () =>
            {
                string path = Controller.GetContainerInstancePath();
                ControllerManager.SetTargetController(path, false);
            });

            HideCommand = new DelegateCommand(async () =>
            {
                if (IsHidden)
                    Controller.Unhide();
                else
                    Controller.Hide();
            });

            CalibrateCommand = new DelegateCommand(async () =>
            {
                Controller.Calibrate();
            });
        }

        private void Controller_StateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
        }

        private void Controller_UserIndexChanged(byte UserIndex)
        {
            OnPropertyChanged(nameof(UserIndex));
        }

        public void Updated()
        {
            OnPropertyChanged(nameof(Controller));
            OnPropertyChanged(nameof(IsHidden));
            OnPropertyChanged(nameof(IsPlugged));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(UserIndex));
            OnPropertyChanged(nameof(CanCalibrate));
        }

        public override void Dispose()
        {
            Controller.UserIndexChanged -= Controller_UserIndexChanged;
            Controller.StateChanged -= Controller_StateChanged;
            base.Dispose();
        }
    }
}
