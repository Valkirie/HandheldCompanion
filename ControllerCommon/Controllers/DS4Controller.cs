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
    public class DS4Controller : DInputController
    {
        public DS4Controller(Joystick joystick, PnPDetails details) : base (joystick, details)
        {
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

            for (int i = 0; i < Controller.Capabilities.ButtonCount; i++)
            {
                if (State.Buttons[i])
                    LogManager.LogDebug("Button {0} pressed", i);
            }

            // todo: implement loop
            if (State.Buttons[0])
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (State.Buttons[1])
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (State.Buttons[2])
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (State.Buttons[3])
                Inputs.Buttons |= ControllerButtonFlags.B4;

            if (State.Buttons[8])
                Inputs.Buttons |= ControllerButtonFlags.Back;
            if (State.Buttons[9])
                Inputs.Buttons |= ControllerButtonFlags.Start;

            if (State.Buttons[6])
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (State.Buttons[7])
                Inputs.Buttons |= ControllerButtonFlags.RightTrigger;

            if (State.Buttons[10])
                Inputs.Buttons |= ControllerButtonFlags.LeftThumb;
            if (State.Buttons[11])
                Inputs.Buttons |= ControllerButtonFlags.RightThumb;

            if (State.Buttons[4])
                Inputs.Buttons |= ControllerButtonFlags.LeftShoulder;
            if (State.Buttons[5])
                Inputs.Buttons |= ControllerButtonFlags.RightShoulder;

            if (State.Buttons[12])
                Inputs.Buttons |= ControllerButtonFlags.Special;
            if (State.Buttons[13])  // TouchpadClick
                Inputs.Buttons |= ControllerButtonFlags.Special;

            switch(State.PointOfViewControllers[0])
            {
                case 0:
                    Inputs.Buttons |= ControllerButtonFlags.DPadUp;
                    break;
                case 4500:
                    Inputs.Buttons |= ControllerButtonFlags.DPadUp;
                    Inputs.Buttons |= ControllerButtonFlags.DPadRight;
                    break;
                case 9000:
                    Inputs.Buttons |= ControllerButtonFlags.DPadRight;
                    break;
                case 13500:
                    Inputs.Buttons |= ControllerButtonFlags.DPadRight;
                    Inputs.Buttons |= ControllerButtonFlags.DPadDown;
                    break;
                case 18000:
                    Inputs.Buttons |= ControllerButtonFlags.DPadDown;
                    break;
                case 22500:
                    Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
                    Inputs.Buttons |= ControllerButtonFlags.DPadDown;
                    break;
                case 27000:
                    Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
                    break;
                case 31500:
                    Inputs.Buttons |= ControllerButtonFlags.DPadUp;
                    Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
                    break;
            }

            Inputs.RightTrigger = State.RotationY * byte.MaxValue / ushort.MaxValue;
            Inputs.LeftTrigger = State.RotationX * byte.MaxValue / ushort.MaxValue;

            Inputs.LeftThumbX = Math.Clamp(State.X - short.MaxValue, short.MinValue, short.MaxValue);
            Inputs.LeftThumbY = Math.Clamp(-State.Y + short.MaxValue, short.MinValue, short.MaxValue);

            Inputs.RightThumbX = Math.Clamp(State.Z - short.MaxValue, short.MinValue, short.MaxValue);
            Inputs.RightThumbY = Math.Clamp(-State.RotationZ + short.MaxValue, short.MinValue, short.MaxValue);

            LogManager.LogDebug("POV {0}", string.Join(',', State.PointOfViewControllers));

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
