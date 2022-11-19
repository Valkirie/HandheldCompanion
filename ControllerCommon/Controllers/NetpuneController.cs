using ControllerCommon.Managers;
using neptune_hidapi.net;
using SharpDX.DirectInput;
using System;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class NetpuneController : IController
    {
        private NeptuneController Controller = new();
        private JoystickState State = new();
        private JoystickState prevState = new();

        private bool isConnected = false;

        public NetpuneController(PnPDetails details)
        {
            Details = details;
            Details.isHooked = true;
        }

        public override string ToString()
        {
            // localize me
            return "Steam Deck Controller";
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
            return isConnected;
        }

        public override async void Rumble()
        {
            base.Rumble();
        }

        public override void Plug()
        {
            try
            {
                Controller.Open();
                isConnected = true;
            }
            catch (Exception)
            {
                return;
            }

            Controller.OnControllerInputReceived = input => Task.Run(() => OnControllerInputReceived(input));

            PipeClient.ServerMessage += OnServerMessage;
            base.Plug();
        }

        private void OnControllerInputReceived(NeptuneControllerInputEventArgs input)
        {
        }

        public override void Unplug()
        {
            try
            {
                Controller.Close();
                isConnected = false;
            }
            catch (Exception)
            {
                return;
            }

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
