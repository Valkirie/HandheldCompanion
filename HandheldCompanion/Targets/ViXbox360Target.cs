using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;

namespace HandheldCompanion.Targets
{
    /// <summary>
    /// ViGEM Xbox360 Controller target.
    /// </summary>
    internal class ViXbox360Target : ViGEmTarget
    {
        private IXbox360Controller? _xboxController;

        protected override string DeviceType => "xbox360";
        protected override int InputLength => 20;

        public ViXbox360Target(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
            HID = HIDmode.Xbox360Controller;

            LogManager.LogInformation("{0} initialized for ViGEM ({1:X4}:{2:X4})", ToString(), vendorId, productId);
        }

        protected override IVirtualGamepad CreateVirtualController()
        {
            try
            {
                if (VirtualManager.vClient == null)
                {
                    LogManager.LogError("VirtualManager.vClient is not initialized");
                    return null;
                }

                var controller = VirtualManager.vClient.CreateXbox360Controller(vendorId, productId);
                _xboxController = (IXbox360Controller)controller;
                _xboxController.AutoSubmitReport = false;
                _xboxController.FeedbackReceived += FeedbackReceived;
                return controller;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create ViGEM Xbox360 controller: {0}", ex.Message);
                return null;
            }
        }

        private void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            SendVibrate(e.LargeMotor, e.SmallMotor);
        }

        public override void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected || _xboxController == null)
                return;

            try
            {
                uint buttons = 0;
                if (inputs.ButtonState[ButtonFlags.DPadUp]) buttons |= 0x0001;
                if (inputs.ButtonState[ButtonFlags.DPadDown]) buttons |= 0x0002;
                if (inputs.ButtonState[ButtonFlags.DPadLeft]) buttons |= 0x0004;
                if (inputs.ButtonState[ButtonFlags.DPadRight]) buttons |= 0x0008;
                if (inputs.ButtonState[ButtonFlags.Start]) buttons |= 0x0010;
                if (inputs.ButtonState[ButtonFlags.Back]) buttons |= 0x0020;
                if (inputs.ButtonState[ButtonFlags.LeftStickClick]) buttons |= 0x0040;
                if (inputs.ButtonState[ButtonFlags.RightStickClick]) buttons |= 0x0080;
                if (inputs.ButtonState[ButtonFlags.L1]) buttons |= 0x0100;
                if (inputs.ButtonState[ButtonFlags.R1]) buttons |= 0x0200;
                if (inputs.ButtonState[ButtonFlags.Special]) buttons |= 0x0400;
                if (inputs.ButtonState[ButtonFlags.B1]) buttons |= 0x1000;
                if (inputs.ButtonState[ButtonFlags.B2]) buttons |= 0x2000;
                if (inputs.ButtonState[ButtonFlags.B3]) buttons |= 0x4000;
                if (inputs.ButtonState[ButtonFlags.B4]) buttons |= 0x8000;

                // D-Pad
                _xboxController.SetButtonState(Xbox360Button.Up, (buttons & 0x0001) != 0);
                _xboxController.SetButtonState(Xbox360Button.Down, (buttons & 0x0002) != 0);
                _xboxController.SetButtonState(Xbox360Button.Left, (buttons & 0x0004) != 0);
                _xboxController.SetButtonState(Xbox360Button.Right, (buttons & 0x0008) != 0);

                // Start/Back
                _xboxController.SetButtonState(Xbox360Button.Start, (buttons & 0x0010) != 0);
                _xboxController.SetButtonState(Xbox360Button.Back, (buttons & 0x0020) != 0);

                // Stick clicks
                _xboxController.SetButtonState(Xbox360Button.LeftThumb, (buttons & 0x0040) != 0);
                _xboxController.SetButtonState(Xbox360Button.RightThumb, (buttons & 0x0080) != 0);

                // Shoulders
                _xboxController.SetButtonState(Xbox360Button.LeftShoulder, (buttons & 0x0100) != 0);
                _xboxController.SetButtonState(Xbox360Button.RightShoulder, (buttons & 0x0200) != 0);

                // Guide button
                _xboxController.SetButtonState(Xbox360Button.Guide, (buttons & 0x0400) != 0);

                // Face buttons (Y, X, B, A)
                _xboxController.SetButtonState(Xbox360Button.Y, (buttons & 0x8000) != 0);
                _xboxController.SetButtonState(Xbox360Button.X, (buttons & 0x4000) != 0);
                _xboxController.SetButtonState(Xbox360Button.B, (buttons & 0x2000) != 0);
                _xboxController.SetButtonState(Xbox360Button.A, (buttons & 0x1000) != 0);

                // Triggers
                _xboxController.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)inputs.AxisState[AxisFlags.L2]);
                _xboxController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)inputs.AxisState[AxisFlags.R2]);

                // Analog sticks
                short lx = (short)inputs.AxisState[AxisFlags.LeftStickX];
                short ly = (short)inputs.AxisState[AxisFlags.LeftStickY];
                short rx = (short)inputs.AxisState[AxisFlags.RightStickX];
                short ry = (short)inputs.AxisState[AxisFlags.RightStickY];

                _xboxController.SetAxisValue(Xbox360Axis.LeftThumbX, lx);
                _xboxController.SetAxisValue(Xbox360Axis.LeftThumbY, ly);
                _xboxController.SetAxisValue(Xbox360Axis.RightThumbX, rx);
                _xboxController.SetAxisValue(Xbox360Axis.RightThumbY, ry);

                _xboxController.SubmitReport();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to update ViGEM Xbox360 controller: {0}", ex.Message);
            }
        }

        protected override void UpdateVirtualController(IVirtualGamepad controller, byte[] reportData)
        {
            // Not used; UpdateInputs handles direct ViGEM API calls
        }

        public override void Dispose()
        {
            try { _xboxController?.Disconnect(); } catch { }
            _xboxController = null;

            base.Dispose();
        }
    }
}
