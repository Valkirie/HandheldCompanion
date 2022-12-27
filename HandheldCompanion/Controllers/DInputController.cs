using ControllerCommon;
using ControllerCommon.Controllers;
using SharpDX.DirectInput;

namespace HandheldCompanion.Controllers
{
    public class DInputController : IController
    {
        protected Joystick Controller;
        protected JoystickState State = new();
        protected JoystickState prevState = new();

        public DInputController(Joystick joystick, PnPDetails details)
        {
            Controller = joystick;
            UserIndex = joystick.Properties.JoystickId;

            Details = details;
            Details.isHooked = true;

            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return Controller.Information.ProductName;
        }

        public override void UpdateReport()
        {
            // update states
            prevState = State;

            base.UpdateReport();
        }

        public override bool IsConnected()
        {
            return (bool)(!Controller?.IsDisposed);
        }

        public override void Plug()
        {
            // Acquire the joystick
            Controller.Acquire();

            base.Plug();
        }

        public override void Unplug()
        {
            // Acquire the joystick
            Controller.Unacquire();

            base.Unplug();
        }
    }
}
