using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace HandheldCompanion.Targets
{
    internal partial class Xbox360Target : ViGEmTarget
    {
        private IXbox360Controller? xboxController;
        public Xbox360Target(ushort vendorId, ushort productId) : base()
        {
            // initialize controller
            virtualController = VirtualManager.vClient.CreateXbox360Controller(vendorId, productId);

            // update HID
            HID = HIDmode.Xbox360Controller;

            xboxController = (IXbox360Controller)virtualController;
            xboxController.AutoSubmitReport = false;
            xboxController.FeedbackReceived += FeedbackReceived;

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            SendVibrate(e.LargeMotor, e.SmallMotor);
        }

        public override unsafe void UpdateInputs(ControllerState Inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected)
                return;

            ushort tempButtons = 0;
            if (Inputs.ButtonState[ButtonFlags.B1]) tempButtons |= Xbox360Button.A.Value;
            if (Inputs.ButtonState[ButtonFlags.B2]) tempButtons |= Xbox360Button.B.Value;
            if (Inputs.ButtonState[ButtonFlags.B3]) tempButtons |= Xbox360Button.X.Value;
            if (Inputs.ButtonState[ButtonFlags.B4]) tempButtons |= Xbox360Button.Y.Value;

            if (Inputs.ButtonState[ButtonFlags.DPadUp]) tempButtons |= Xbox360Button.Up.Value;
            if (Inputs.ButtonState[ButtonFlags.DPadDown]) tempButtons |= Xbox360Button.Down.Value;
            if (Inputs.ButtonState[ButtonFlags.DPadLeft]) tempButtons |= Xbox360Button.Left.Value;
            if (Inputs.ButtonState[ButtonFlags.DPadRight]) tempButtons |= Xbox360Button.Right.Value;

            if (Inputs.ButtonState[ButtonFlags.Back]) tempButtons |= Xbox360Button.Back.Value;
            if (Inputs.ButtonState[ButtonFlags.Start]) tempButtons |= Xbox360Button.Start.Value;

            if (Inputs.ButtonState[ButtonFlags.L1]) tempButtons |= Xbox360Button.LeftShoulder.Value;
            if (Inputs.ButtonState[ButtonFlags.R1]) tempButtons |= Xbox360Button.RightShoulder.Value;

            if (Inputs.ButtonState[ButtonFlags.LeftStickClick]) tempButtons |= Xbox360Button.LeftThumb.Value;
            if (Inputs.ButtonState[ButtonFlags.RightStickClick]) tempButtons |= Xbox360Button.RightThumb.Value;

            if (Inputs.ButtonState[ButtonFlags.Special]) tempButtons |= Xbox360Button.Guide.Value;

            try
            {
                if (xboxController is null)
                    return;

                xboxController.SetAxisValue(Xbox360Axis.LeftThumbX, Inputs.AxisState[AxisFlags.LeftStickX]);
                xboxController.SetAxisValue(Xbox360Axis.LeftThumbY, Inputs.AxisState[AxisFlags.LeftStickY]);
                xboxController.SetAxisValue(Xbox360Axis.RightThumbX, Inputs.AxisState[AxisFlags.RightStickX]);
                xboxController.SetAxisValue(Xbox360Axis.RightThumbY, Inputs.AxisState[AxisFlags.RightStickY]);
                xboxController.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)Inputs.AxisState[AxisFlags.L2]);
                xboxController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)Inputs.AxisState[AxisFlags.R2]);
                xboxController.SetButtonsFull(tempButtons);
                xboxController.SubmitReport();
            }
            catch (VigemBusNotFoundException ex)
            {
                LogManager.LogError(ex.Message);
            }
            catch (VigemInvalidTargetException ex)
            {
                LogManager.LogError(ex.Message);
            }
        }

        public override void Dispose()
        {
            try { xboxController?.Disconnect(); } catch { }
            xboxController = null;

            base.Dispose();
        }
    }
}