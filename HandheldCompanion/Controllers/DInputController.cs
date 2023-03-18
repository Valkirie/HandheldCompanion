using ControllerCommon;
using ControllerCommon.Controllers;
using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers
{
    public class DInputController : IController
    {
        public Joystick joystick;
        protected JoystickState State = new();
        protected JoystickState prevState = new();

        public DInputController(Joystick joystick, PnPDetails details)
        {
            if (joystick is null)
                return;

            this.joystick = joystick;
            UserIndex = joystick.Properties.JoystickId;

            if (details is null)
                return;

            Details = details;
            Details.isHooked = true;

            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;

            // ui
            DrawControls();
            RefreshControls();
        }

        public override string ToString()
        {
            string baseName = base.ToString();
            if (!string.IsNullOrEmpty(baseName))
                return baseName;
            else if (joystick is null)
                return string.Empty;
            else
                return joystick.Information.ProductName;
        }

        public override void UpdateInputs(long ticks)
        {
            // update states
            prevState = State;

            base.UpdateInputs(ticks);
        }

        public override bool IsConnected()
        {
            return (bool)(!joystick?.IsDisposed);
        }

        public override void Plug()
        {
            joystick.Acquire();

            base.Plug();
        }

        public override void Unplug()
        {
            // Unacquire the joystick
            joystick.Unacquire();

            base.Unplug();
        }
    }
}
