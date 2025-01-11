using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using System.Threading.Tasks;
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

        // Encapsulating null checks for better readability
        private bool HasController => _controller is not null;
        public string Name => HasController ? _controller.ToString() : "N/A";
        public int UserIndex => HasController ? _controller.GetUserIndex() : 0;
        public bool CanCalibrate => HasController && _controller.HasMotionSensor();
        public string Enumerator => HasController ? _controller.GetEnumerator() : "USB";
        public bool IsBusy => HasController && _controller.IsBusy;
        public bool IsVirtual => HasController && _controller.IsVirtual();
        public bool IsPlugged => HasController && ControllerManager.IsTargetController(_controller.GetInstanceId());
        public bool IsHidden => HasController && _controller.IsHidden();
        public bool IsInternal => HasController && _controller.IsInternal();
        public bool IsWireless => HasController && _controller.IsWireless();
        public bool IsDongle => HasController && _controller.IsDongle();

        public ICommand ConnectCommand { get; private set; }
        public ICommand HideCommand { get; private set; }
        public ICommand CalibrateCommand { get; private set; }

        public ControllerViewModel() { }

        public ControllerViewModel(IController controller)
        {
            Controller = controller;
            Controller.UserIndexChanged += Controller_UserIndexChanged;
            Controller.StateChanged += Controller_StateChanged;
            Controller.VisibilityChanged += Controller_VisibilityChanged;

            ConnectCommand = new DelegateCommand(async () =>
            {
                string path = Controller.GetContainerInstanceId();
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

        private async void Controller_VisibilityChanged(bool status)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            OnPropertyChanged(nameof(IsHidden));
        }

        public void Updated()
        {
            // refresh all properties
            OnPropertyChanged(string.Empty);
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
            Controller.VisibilityChanged -= Controller_VisibilityChanged;

            base.Dispose();
        }
    }
}
