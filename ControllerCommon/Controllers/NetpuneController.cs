using neptune_hidapi.net;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class NetpuneController : IController
    {
        private NeptuneController Controller = new();
        private NeptuneControllerInputState State;
        private NeptuneControllerInputState prevState;

        private bool isConnected = false;

        public NetpuneController(PnPDetails details)
        {
            Details = details;
            Details.isHooked = true;
        }

        public override string ToString()
        {
            return Details.DeviceDesc;
        }

        public override void UpdateReport()
        {
        }

        public override bool IsConnected()
        {
            return isConnected;
        }

        public override async void Rumble()
        {
            for (int i = 0; i < 2; i++)
            {
                SetVibration(ushort.MaxValue, ushort.MaxValue);
                await Task.Delay(100);

                SetVibration(0, 0);
                await Task.Delay(100);
            }
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
            if (prevState.GetHashCode() == State.GetHashCode())
                return;

            Inputs.Buttons = InjectedButtons;

            if (input.State.ButtonState[NeptuneControllerButton.BtnA])
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (input.State.ButtonState[NeptuneControllerButton.BtnB])
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnX])
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (input.State.ButtonState[NeptuneControllerButton.BtnY])
                Inputs.Buttons |= ControllerButtonFlags.B4;

            if (input.State.ButtonState[NeptuneControllerButton.BtnOptions])
                Inputs.Buttons |= ControllerButtonFlags.Start;
            if (input.State.ButtonState[NeptuneControllerButton.BtnMenu])
                Inputs.Buttons |= ControllerButtonFlags.Back;

            if (input.State.ButtonState[NeptuneControllerButton.BtnSteam])
                Inputs.Buttons |= ControllerButtonFlags.Special;
            if (input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess])
                Inputs.Buttons |= ControllerButtonFlags.OEM1;

            if (input.State.AxesState[NeptuneControllerAxis.L2] > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (input.State.AxesState[NeptuneControllerAxis.R2] > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.RightTrigger;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickPress])
                Inputs.Buttons |= ControllerButtonFlags.LeftThumb;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickPress])
                Inputs.Buttons |= ControllerButtonFlags.RightThumb;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM3;

            if (input.State.ButtonState[NeptuneControllerButton.BtnL1])
                Inputs.Buttons |= ControllerButtonFlags.LeftShoulder;
            if (input.State.ButtonState[NeptuneControllerButton.BtnR1])
                Inputs.Buttons |= ControllerButtonFlags.RightShoulder;

            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadUp])
                Inputs.Buttons |= ControllerButtonFlags.DPadUp;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadDown])
                Inputs.Buttons |= ControllerButtonFlags.DPadDown;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft])
                Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadRight])
                Inputs.Buttons |= ControllerButtonFlags.DPadRight;

            // Left Stick
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickUp;

            Inputs.LeftThumbX = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
            Inputs.LeftThumbY = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

            // Right Stick
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickUp;

            Inputs.RightThumbX = input.State.AxesState[NeptuneControllerAxis.RightStickX];
            Inputs.RightThumbY = input.State.AxesState[NeptuneControllerAxis.RightStickY];

            Inputs.LeftTrigger = input.State.AxesState[NeptuneControllerAxis.L2];
            Inputs.RightTrigger = input.State.AxesState[NeptuneControllerAxis.R2];

            // todo: map trackpad(s)

            // update states
            prevState = State;

            base.UpdateReport();
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

        public override void SetVibration(ushort LargeMotor, ushort SmallMotor)
        {
            ushort LeftMotorSpeed = (ushort)((LargeMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);
            ushort RightMotorSpeed = (ushort)((SmallMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);

            // most likely incorrect
            // todo: https://github.com/mKenfenheuer/steam-deck-windows-usermode-driver/blob/69ce8085d3b6afe888cb2e36bd95836cea58084a/SWICD/Services/ControllerService.cs
            Controller.SetHaptic(1, LeftMotorSpeed, 30, 0);
            Controller.SetHaptic(0, RightMotorSpeed, 30, 0);
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_VIBRATION:
                    {
                        PipeClientVibration e = (PipeClientVibration)message;
                        SetVibration(e.LargeMotor, e.SmallMotor);
                    }
                    break;
            }
        }
    }
}
