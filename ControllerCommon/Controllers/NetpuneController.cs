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
    public class NetpuneController : IController
    {
        private Joystick Controller;
        private JoystickState State = new();
        private JoystickState prevState = new();

        public NetpuneController(PnPDetails details)
        {
            Details = details;
            Details.isHooked = true;

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
