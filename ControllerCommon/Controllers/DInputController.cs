using ControllerCommon.Managers;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class DInputController : IController
    {
        private Joystick Controller;
        private JoystickState State = new();
        private JoystickState prevState = new();

        public DInputController(Joystick joystick, PnPDetails details)
        {
            Controller = joystick;
            UserIndex = joystick.Properties.JoystickId;

            Details = details;
            Details.isHooked = true;

            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;

            if (!IsConnected())
                return;

            UpdateTimer.Tick += (sender, e) => UpdateReport();
        }

        public override string ToString()
        {
            return Controller.Information.ProductName;
        }

        public override void UpdateReport()
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            // Poll events from joystick
            Controller.Poll();

            // update gamepad state
            State = Controller.GetCurrentState();

            if (prevState.GetHashCode() == State.GetHashCode() && prevInjectedButtons == InjectedButtons)
                return;

            Inputs.Buttons = InjectedButtons;

            // todo: implement loop
            if (State.Buttons[0])
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (State.Buttons[1])
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (State.Buttons[2])
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (State.Buttons[3])
                Inputs.Buttons |= ControllerButtonFlags.B4;
            if (State.Buttons[4])
                Inputs.Buttons |= ControllerButtonFlags.B5;
            if (State.Buttons[5])
                Inputs.Buttons |= ControllerButtonFlags.B6;
            if (State.Buttons[6])
                Inputs.Buttons |= ControllerButtonFlags.B7;
            if (State.Buttons[7])
                Inputs.Buttons |= ControllerButtonFlags.B8;

            base.UpdateReport();
        }

        public override bool IsConnected()
        {
            return (bool)(!Controller?.IsDisposed);
        }

        public override async void Rumble()
        {
            base.Rumble();
        }

        public override void Plug()
        {
            // Acquire the joystick
            Controller.Acquire();

            PipeClient.ServerMessage += OnServerMessage;
            base.Plug();
        }

        public override void Unplug()
        {
            // Acquire the joystick
            Controller.Unacquire();

            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_VIBRATION:
                    {
                        PipeClientVibration e = (PipeClientVibration)message;

                        ushort LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);
                        ushort RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);
                    }
                    break;
            }
        }
    }
}
