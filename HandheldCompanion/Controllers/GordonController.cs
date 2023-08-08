using HandheldCompanion.Managers;
using steam_hidapi.net;
using steam_hidapi.net.Hid;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ControllerCommon.Controllers;
using ControllerCommon;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using System.Windows.Media;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using ControllerCommon.Actions;

namespace HandheldCompanion.Controllers
{
    public class GordonController : SteamController
    {
        private steam_hidapi.net.GordonController Controller;
        private GordonControllerInputEventArgs input;

        private const short TrackPadInner = short.MaxValue / 2;
        public const ushort MaxRumbleIntensity = 2048;

        public GordonController(PnPDetails details) : base()
        {
            if (details is null)
                return;

            Controller = new(details.attributes.VendorID, details.attributes.ProductID, details.GetMI());

            // open controller
            Open();

            Details = details;
            Details.isHooked = true;

            // UI
            ColoredButtons.Add(ButtonFlags.B1, new SolidColorBrush(Color.FromArgb(255, 81, 191, 61)));
            ColoredButtons.Add(ButtonFlags.B2, new SolidColorBrush(Color.FromArgb(255, 217, 65, 38)));
            ColoredButtons.Add(ButtonFlags.B3, new SolidColorBrush(Color.FromArgb(255, 26, 159, 255)));
            ColoredButtons.Add(ButtonFlags.B4, new SolidColorBrush(Color.FromArgb(255, 255, 200, 44)));

            // UI
            DrawControls();
            RefreshControls();

            // Additional controller specific source buttons/axes
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.L4, ButtonFlags.R4 });
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight });
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight });

            SourceAxis.Add(AxisLayoutFlags.LeftPad);
            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);

            // This is a very original controller, it doesn't have few things
            SourceButtons.Remove(ButtonFlags.RightStickClick);
            SourceButtons.Remove(ButtonFlags.RightStickUp);
            SourceButtons.Remove(ButtonFlags.RightStickDown);
            SourceButtons.Remove(ButtonFlags.RightStickLeft);
            SourceButtons.Remove(ButtonFlags.RightStickRight);

            SourceAxis.Remove(AxisLayoutFlags.RightStick);
        }

        public override string ToString()
        {
            string baseName = base.ToString();
            if (!string.IsNullOrEmpty(baseName))
                return baseName;
            return "Steam Controller Gordon";
        }

        public override void UpdateInputs(long ticks)
        {
            if (input is null)
                return;

            Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

            Inputs.ButtonState[ButtonFlags.B1] = input.State.ButtonState[GordonControllerButton.BtnA];
            Inputs.ButtonState[ButtonFlags.B2] = input.State.ButtonState[GordonControllerButton.BtnB];
            Inputs.ButtonState[ButtonFlags.B3] = input.State.ButtonState[GordonControllerButton.BtnX];
            Inputs.ButtonState[ButtonFlags.B4] = input.State.ButtonState[GordonControllerButton.BtnY];
            
            Inputs.ButtonState[ButtonFlags.DPadUp] = input.State.ButtonState[GordonControllerButton.BtnDpadUp];
            Inputs.ButtonState[ButtonFlags.DPadDown] = input.State.ButtonState[GordonControllerButton.BtnDpadDown];
            Inputs.ButtonState[ButtonFlags.DPadLeft] = input.State.ButtonState[GordonControllerButton.BtnDpadLeft];
            Inputs.ButtonState[ButtonFlags.DPadRight] = input.State.ButtonState[GordonControllerButton.BtnDpadRight];

            Inputs.ButtonState[ButtonFlags.Start] = input.State.ButtonState[GordonControllerButton.BtnOptions];
            Inputs.ButtonState[ButtonFlags.Back] = input.State.ButtonState[GordonControllerButton.BtnMenu];
            Inputs.ButtonState[ButtonFlags.Special] = input.State.ButtonState[GordonControllerButton.BtnSteam];

            var L2 = input.State.AxesState[GordonControllerAxis.L2];
            var R2 = input.State.AxesState[GordonControllerAxis.R2];

            Inputs.ButtonState[ButtonFlags.L2Soft] = L2 > Gamepad.TriggerThreshold;
            Inputs.ButtonState[ButtonFlags.R2Soft] = R2 > Gamepad.TriggerThreshold;

            Inputs.ButtonState[ButtonFlags.L2Full] = L2 > Gamepad.TriggerThreshold * 8;
            Inputs.ButtonState[ButtonFlags.R2Full] = R2 > Gamepad.TriggerThreshold * 8;

            Inputs.AxisState[AxisFlags.L2] = (short)L2;
            Inputs.AxisState[AxisFlags.R2] = (short)R2;

            Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[GordonControllerButton.BtnL1];
            Inputs.ButtonState[ButtonFlags.R1] = input.State.ButtonState[GordonControllerButton.BtnR1];
            Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[GordonControllerButton.BtnL4];
            Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[GordonControllerButton.BtnR4];

            // Left Stick
            Inputs.ButtonState[ButtonFlags.LeftStickClick] = input.State.ButtonState[GordonControllerButton.BtnLStickPress];

            Inputs.AxisState[AxisFlags.LeftThumbX] = input.State.AxesState[GordonControllerAxis.LeftStickX];
            Inputs.AxisState[AxisFlags.LeftThumbY] = input.State.AxesState[GordonControllerAxis.LeftStickY];

            Inputs.ButtonState[ButtonFlags.LeftStickLeft] = Inputs.AxisState[AxisFlags.LeftThumbX] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickRight] = Inputs.AxisState[AxisFlags.LeftThumbX] > Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickDown] = Inputs.AxisState[AxisFlags.LeftThumbY] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickUp] = Inputs.AxisState[AxisFlags.LeftThumbY] > Gamepad.LeftThumbDeadZone;

            // TODO: Implement Inner/Outer Ring button mappings for sticks
            // https://github.com/Havner/HandheldCompanion/commit/e1124ceb6c59051201756d5e95b2eb39a3bb24f6

            /* float leftLength = new Vector2(Inputs.AxisState[AxisFlags.LeftThumbX], Inputs.AxisState[AxisFlags.LeftThumbY]).Length();
            Inputs.ButtonState[ButtonFlags.LeftStickOuterRing] = leftLength >= (RingThreshold * short.MaxValue);
            Inputs.ButtonState[ButtonFlags.LeftStickInnerRing] = leftLength >= Gamepad.LeftThumbDeadZone && leftLength < (RingThreshold * short.MaxValue); */

            // Left Pad
            Inputs.ButtonState[ButtonFlags.LeftPadTouch] = input.State.ButtonState[GordonControllerButton.BtnLPadTouch];
            Inputs.ButtonState[ButtonFlags.LeftPadClick] = input.State.ButtonState[GordonControllerButton.BtnLPadPress];

            if (Inputs.ButtonState[ButtonFlags.LeftPadTouch])
            {
                Inputs.AxisState[AxisFlags.LeftPadX] = input.State.AxesState[GordonControllerAxis.LeftPadX];
                Inputs.AxisState[AxisFlags.LeftPadY] = input.State.AxesState[GordonControllerAxis.LeftPadY];
            }
            else
            {
                Inputs.AxisState[AxisFlags.LeftPadX] = 0;
                Inputs.AxisState[AxisFlags.LeftPadY] = 0;
            }

            if (Inputs.ButtonState[ButtonFlags.LeftPadClick])
            {
                InputUtils.TouchToDirections(Inputs.AxisState[AxisFlags.LeftPadX], Inputs.AxisState[AxisFlags.LeftPadY], TrackPadInner, 0, out bool[] buttons);
                Inputs.ButtonState[ButtonFlags.LeftPadClickUp] = buttons[0];
                Inputs.ButtonState[ButtonFlags.LeftPadClickRight] = buttons[1];
                Inputs.ButtonState[ButtonFlags.LeftPadClickDown] = buttons[2];
                Inputs.ButtonState[ButtonFlags.LeftPadClickLeft] = buttons[3];
            }
            else
            {
                Inputs.ButtonState[ButtonFlags.LeftPadClickUp] = false;
                Inputs.ButtonState[ButtonFlags.LeftPadClickRight] = false;
                Inputs.ButtonState[ButtonFlags.LeftPadClickDown] = false;
                Inputs.ButtonState[ButtonFlags.LeftPadClickLeft] = false;
            }

            // Right Pad
            Inputs.ButtonState[ButtonFlags.RightPadTouch] = input.State.ButtonState[GordonControllerButton.BtnRPadTouch];
            Inputs.ButtonState[ButtonFlags.RightPadClick] = input.State.ButtonState[GordonControllerButton.BtnRPadPress];

            if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
            {
                Inputs.AxisState[AxisFlags.RightPadX] = input.State.AxesState[GordonControllerAxis.RightPadX];
                Inputs.AxisState[AxisFlags.RightPadY] = input.State.AxesState[GordonControllerAxis.RightPadY];
            }
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
            }

            if (Inputs.ButtonState[ButtonFlags.RightPadClick])
            {
                InputUtils.TouchToDirections(Inputs.AxisState[AxisFlags.RightPadX], Inputs.AxisState[AxisFlags.RightPadY], TrackPadInner, 0, out bool[] buttons);
                Inputs.ButtonState[ButtonFlags.RightPadClickUp] = buttons[0];
                Inputs.ButtonState[ButtonFlags.RightPadClickRight] = buttons[1];
                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = buttons[2];
                Inputs.ButtonState[ButtonFlags.RightPadClickLeft] = buttons[3];
            }
            else
            {
                Inputs.ButtonState[ButtonFlags.RightPadClickUp] = false;
                Inputs.ButtonState[ButtonFlags.RightPadClickRight] = false;
                Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;
                Inputs.ButtonState[ButtonFlags.RightPadClickLeft] = false;
            }

            base.UpdateInputs(ticks);
        }

        public override void UpdateMovements(long ticks)
        {
            if (input is null)
                return;

            // TODO: why Z/Y swapped?
            Movements.GyroAccelZ = -(float)input.State.AxesState[GordonControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Movements.GyroAccelY = -(float)input.State.AxesState[GordonControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            Movements.GyroAccelX = -(float)input.State.AxesState[GordonControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;

            // TODO: why Roll/Pitch swapped?
            Movements.GyroPitch = -(float)input.State.AxesState[GordonControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;
            Movements.GyroRoll = (float)input.State.AxesState[GordonControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;
            Movements.GyroYaw = -(float)input.State.AxesState[GordonControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;
            
            base.UpdateMovements(ticks);
        }

        private void OnControllerInputReceived(GordonControllerInputEventArgs input)
        {
            this.input = input;
        }

        public override void Plug()
        {
            try
            {
                Controller.OnControllerInputReceived = input => Task.Run(() => OnControllerInputReceived(input));

                // open controller
                Open();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize GordonController. Exception: {0}", ex.Message);
                return;
            }

            Controller.SetLizardMode(false);
            Controller.SetGyroscope(true);
            Controller.SetIdleTimeout(300);  // ~5 min

            SetVirtualMuted(SettingsManager.GetBoolean("SteamMuteController"));

            PipeClient.ServerMessage += OnServerMessage;

            TimerManager.Tick += UpdateInputs;
            TimerManager.Tick += UpdateMovements;

            base.Plug();
        }

        public override void Unplug()
        {
            try
            {
                // restore lizard state
                Controller.SetLizardMode(true);
                Controller.SetGyroscope(false);
                Controller.SetIdleTimeout(0);
                //Controller.TurnOff();  // TODO: why not?

                // close controller
                Close();
            }
            catch
            {
                return;
            }

            TimerManager.Tick -= UpdateInputs;
            TimerManager.Tick -= UpdateMovements;

            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        private void Open()
        {
            try
            {
                Controller.Open();
                isConnected = true;
            }
            catch { }
        }

        private void Close()
        {
            try
            {
                Controller.Close();
                isConnected = false;
            }
            catch { }
        }

        public override void Cleanup()
        {
            TimerManager.Tick -= UpdateInputs;
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_VIBRATION:
                    {
                        var e = (PipeClientVibration)message;
                        SetVibration(e.LargeMotor, e.SmallMotor);
                    }
                    break;
            }
        }

        public override void Rumble(int Loop = 1, byte LeftValue = byte.MaxValue, byte RightValue = byte.MaxValue,
            byte Duration = 125)
        {
            Task.Factory.StartNew(async () =>
            {
                for (var i = 0; i < Loop * 2; i++)
                {
                    if (i % 2 == 0)
                        SetVibration(LeftValue, RightValue);
                    else
                        SetVibration(0, 0);

                    await Task.Delay(Duration);
                }
            });
        }

        public ushort GetHapticIntensity(byte input, ushort maxIntensity)
        {
            return (ushort)(input * maxIntensity * VibrationStrength / 255);
        }

        public override void SetVibrationStrength(double value, bool rumble)
        {
            base.SetVibrationStrength(value, rumble);
            if (rumble)
                Rumble();
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            ushort leftAmplitude = GetHapticIntensity(LargeMotor, MaxRumbleIntensity);
            Controller.SetHaptic((byte)SCHapticMotor.Left, leftAmplitude, 0, 1);

            ushort rightAmplitude = GetHapticIntensity(SmallMotor, MaxRumbleIntensity);
            Controller.SetHaptic((byte)SCHapticMotor.Right, rightAmplitude, 0, 1);
        }

        public override void SetHaptic(HapticStrength strength, ButtonFlags button)
        {
            ushort value = strength switch
            {
                HapticStrength.Low => 512,
                HapticStrength.Medium => 1024,
                HapticStrength.High => 2048,
                _ => 0,
            };
            Controller.SetHaptic((byte)GetMotorForButton(button), value, 0, 1);
        }
    }
}