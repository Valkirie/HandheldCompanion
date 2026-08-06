using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.GameSir;
using HandheldCompanion.Controllers.Lenovo;
using HandheldCompanion.Managers;
using System.Threading.Tasks;
using System.Windows.Input;
using static SDL3.SDL;

namespace HandheldCompanion.ViewModels
{
    public class ControllerViewModel : BaseViewModel
    {
        private IController? _controller;
        public IController? Controller
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
        public string Name => _controller?.ToString() ?? "N/A";
        public int UserIndex => _controller?.GetUserIndex() ?? 0;
        public bool CanCalibrate => _controller?.HasMotionSensor() == true;
        public bool HasLayout => HasController && _controller is TarantulaProController;
        public string Enumerator => _controller?.GetEnumerator() ?? "USB";
        public bool IsBusy => _controller?.IsBusy == true;
        public bool IsVirtual => _controller?.IsVirtual() == true;
        public bool IsPlugged => _controller?.IsPlugged == true;
        public bool IsHidden => _controller?.IsHidden() == true;
        public bool IsInternal => _controller?.IsInternal() == true;
        public bool IsWireless => _controller?.IsWireless() == true;
        public bool IsDongle => _controller?.IsDongle() == true;
        public bool IsLegionWireless => _controller is LegionController && _controller.IsWireless();
        public int VisibleUserIndexCount => _controller is XInputController or LegionControllerXInput ? 4 : 8;

        private string _LayoutGlyph = "\ue001"; // Default icon for layout
        public string LayoutGlyph
        {
            get
            {
                return _LayoutGlyph;
            }
            set
            {
                if (_LayoutGlyph != value)
                {
                    _LayoutGlyph = value;
                    OnPropertyChanged(nameof(LayoutGlyph));
                }
            }
        }

        public string ControllerGlyph
        {
            get
            {
                if (Controller is TarantulaProController)
                {
                    return "\u243C";    // generic icon
                }
                else if (Controller is XInputController or LegionControllerXInput)
                {
                    return "\u2442";    // Xbox360 icon
                }
                else if (Controller is SDLController sdlController)
                {
                    switch (sdlController.GamepadType)
                    {
                        default:
                            return "\u243C";    // generic icon
                        case GamepadType.Xbox360:
                        case GamepadType.XboxOne:
                            return "\u2442";    // Xbox360 icon
                        case GamepadType.PS3:
                        case GamepadType.PS4:
                            return "\u2440";    // DualShock4 icon
                        case GamepadType.PS5:
                            return "\u2441";    // DualSense icon
                    }
                }

                return "\u243C";    // generic icon
            }
        }

        public ICommand ConnectCommand { get; private set; } = null!;
        public ICommand HideCommand { get; private set; } = null!;
        public ICommand CalibrateCommand { get; private set; } = null!;
        public ICommand SwitchLayoutCommand { get; private set; } = null!;

        public ControllerViewModel() { }

        public ControllerViewModel(IController controller)
        {
            DisposeController();

            Controller = controller;
            Controller.UserIndexChanged += Controller_UserIndexChanged;
            Controller.StateChanged += Controller_StateChanged;
            Controller.VisibilityChanged += Controller_VisibilityChanged;

            if (Controller is TarantulaProController proController)
                proController.OnLayoutChanged += ProController_OnLayoutChanged;

            ConnectCommand = new DelegateCommand(async () =>
            {
                string path = Controller?.GetContainerInstanceId() ?? string.Empty;
                if (!string.IsNullOrEmpty(path))
                    await Task.Run(() => ControllerManager.SetTargetController(path, false));
            });

            HideCommand = new DelegateCommand(async () =>
            {
                await Task.Run(() =>
                {
                    if (IsHidden)
                        Controller?.Unhide();
                    else
                        Controller?.Hide();
                });
            });

            CalibrateCommand = new DelegateCommand(async () =>
            {
                Controller?.Calibrate();
            });

            SwitchLayoutCommand = new DelegateCommand(async () =>
            {
                if (Controller is TarantulaProController tarantulaProController)
                    tarantulaProController.SwitchLayout();
            });
        }

        private void ProController_OnLayoutChanged(TarantulaProController.ButtonLayout buttonLayout)
        {
            LayoutGlyph = buttonLayout == TarantulaProController.ButtonLayout.Xbox ? "\ue001" : "\ue002";
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
            OnPropertyChanged(nameof(VisibleUserIndexCount));

            // controller specific properties
            OnPropertyChanged(nameof(HasLayout));
            OnPropertyChanged(nameof(LayoutGlyph));
        }

        private void DisposeController()
        {
            // clear previous events
            if (_controller is not null)
            {
                _controller.UserIndexChanged -= Controller_UserIndexChanged;
                _controller.StateChanged -= Controller_StateChanged;
                _controller.VisibilityChanged -= Controller_VisibilityChanged;

                if (_controller is TarantulaProController proController)
                    proController.OnLayoutChanged -= ProController_OnLayoutChanged;
            }

            // clear controller
            _controller = null;
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeController();
            }

            base.Dispose(disposing);
        }
    }
}
