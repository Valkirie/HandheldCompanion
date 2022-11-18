using ControllerCommon.Managers;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
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
            return Controller.Information.ProductName;
        }

        public override void UpdateReport()
        {
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
