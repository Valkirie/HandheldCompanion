﻿using HandheldCompanion.Actions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using SharpDX.XInput;
using steam_hidapi.net;
using steam_hidapi.net.Hid;
using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HandheldCompanion.Controllers
{
    public class GordonController : SteamController
    {
        private steam_hidapi.net.GordonController Controller;
        private GordonControllerInputEventArgs input;

        private const short TrackPadInner = short.MaxValue / 2;
        public const ushort MaxRumbleIntensity = 2048;

        public GordonController()
        { }

        public GordonController(PnPDetails details) : base()
        {
            AttachDetails(details);

            // UI
            ColoredButtons.Add(ButtonFlags.B1, Color.FromArgb(255, 81, 191, 61));
            ColoredButtons.Add(ButtonFlags.B2, Color.FromArgb(255, 217, 65, 38));
            ColoredButtons.Add(ButtonFlags.B3, Color.FromArgb(255, 26, 159, 255));
            ColoredButtons.Add(ButtonFlags.B4, Color.FromArgb(255, 255, 200, 44));
        }

        protected override void InitializeInputOutput()
        {
            // Additional controller specific source buttons/axes
            SourceButtons.AddRange([ButtonFlags.L4, ButtonFlags.R4]);
            SourceButtons.AddRange([ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight]);
            SourceButtons.AddRange([ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight]);

            SourceAxis.Add(AxisLayoutFlags.LeftPad);
            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);

            TargetButtons.Add(ButtonFlags.LeftPadClick);
            TargetButtons.Add(ButtonFlags.RightPadClick);
            TargetButtons.Add(ButtonFlags.LeftPadTouch);
            TargetButtons.Add(ButtonFlags.RightPadTouch);

            TargetAxis.Add(AxisLayoutFlags.LeftPad);
            TargetAxis.Add(AxisLayoutFlags.RightPad);

            // This is a very original controller, it doesn't have few things
            SourceButtons.Remove(ButtonFlags.RightStickClick);
            SourceButtons.Remove(ButtonFlags.RightStickUp);
            SourceButtons.Remove(ButtonFlags.RightStickDown);
            SourceButtons.Remove(ButtonFlags.RightStickLeft);
            SourceButtons.Remove(ButtonFlags.RightStickRight);

            SourceAxis.Remove(AxisLayoutFlags.RightStick);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = IsConnected();
            if (WasPlugged) Close();

            // create controller
            Controller = new(details.VendorID, details.ProductID, details.GetMI());
            UserIndex = (byte)details.GetMI();

            // (re)plug controller if needed
            if (WasPlugged) Open();
        }

        public override string ToString()
        {
            string baseName = base.ToString();
            if (!string.IsNullOrEmpty(baseName))
                return baseName;
            return "Steam Controller Gordon";
        }

        public override bool IsConnected()
        {
            return Controller?.Reading == true && Controller?.IsDeviceValid == true;
        }

        public override void UpdateInputs(long ticks, float delta)
        {
            if (input is null || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

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

            Inputs.AxisState[AxisFlags.L2] = L2;
            Inputs.AxisState[AxisFlags.R2] = R2;

            Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[GordonControllerButton.BtnL1];
            Inputs.ButtonState[ButtonFlags.R1] = input.State.ButtonState[GordonControllerButton.BtnR1];
            Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[GordonControllerButton.BtnL4];
            Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[GordonControllerButton.BtnR4];

            // Left Stick
            Inputs.ButtonState[ButtonFlags.LeftStickClick] = input.State.ButtonState[GordonControllerButton.BtnLStickPress];

            Inputs.AxisState[AxisFlags.LeftStickX] = input.State.AxesState[GordonControllerAxis.LeftStickX];
            Inputs.AxisState[AxisFlags.LeftStickY] = input.State.AxesState[GordonControllerAxis.LeftStickY];

            Inputs.ButtonState[ButtonFlags.LeftStickLeft] = Inputs.AxisState[AxisFlags.LeftStickX] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickRight] = Inputs.AxisState[AxisFlags.LeftStickX] > Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickDown] = Inputs.AxisState[AxisFlags.LeftStickY] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickUp] = Inputs.AxisState[AxisFlags.LeftStickY] > Gamepad.LeftThumbDeadZone;

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

            // Accelerometer has 16 bit resolution and a range of +/- 2g
            float aX = (float)input.State.AxesState[GordonControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;
            float aY = (float)input.State.AxesState[GordonControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            float aZ = -(float)input.State.AxesState[GordonControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;

            // Gyroscope has 16 bit resolution and a range of +/- 2000 dps
            float gX = (float)input.State.AxesState[GordonControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;  // Roll
            float gY = (float)input.State.AxesState[GordonControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;   // Pitch
            float gZ = (float)input.State.AxesState[GordonControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;    // Yaw

            // store motion
            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            // process motion
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);

            base.UpdateInputs(ticks, delta);
        }

        private void Open()
        {
            try
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived += HandleControllerInput;

                    Controller.SetLizardMode(false);
                    Controller.SetGyroscope(true);
                    Controller.SetIdleTimeout(300);  // ~5 min

                    // open controller
                    Controller.Open();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(GordonController), ex.Message);
                return;
            }
        }

        private void Close()
        {
            try
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= HandleControllerInput;

                    // restore lizard state
                    Controller.SetLizardMode(true);
                    Controller.SetGyroscope(false);
                    Controller.SetIdleTimeout(0);

                    // close controller
                    Controller.Close();
                }
            }
            catch
            {
                return;
            }
        }

        public override void Gone()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= HandleControllerInput;
                    Controller.EndRead();
                    Controller = null;
                }
            }
        }

        public override void Plug()
        {
            Open();

            TimerManager.Tick += UpdateInputs;

            base.Plug();
        }

        public override void Unplug()
        {
            Close();

            TimerManager.Tick -= UpdateInputs;
            base.Unplug();
        }

        private Task HandleControllerInput(GordonControllerInputEventArgs input)
        {
            this.input = input;
            return Task.CompletedTask;
        }

        public ushort GetHapticIntensity(byte input, ushort maxIntensity)
        {
            return (ushort)(input * maxIntensity * VibrationStrength / 255);
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