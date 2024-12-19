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
        public Xbox360Target(ushort vendorId, ushort productId) : base()
        {
            // initialize controller
            HID = HIDmode.Xbox360Controller;

            virtualController = VirtualManager.vClient.CreateXbox360Controller(vendorId, productId);
            virtualController.AutoSubmitReport = false;
            ((IXbox360Controller)virtualController).FeedbackReceived += FeedbackReceived;

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

            ((IXbox360Controller)virtualController).SetAxisValue(Xbox360Axis.LeftThumbX, Inputs.AxisState[AxisFlags.LeftStickX]);
            ((IXbox360Controller)virtualController).SetAxisValue(Xbox360Axis.LeftThumbY, Inputs.AxisState[AxisFlags.LeftStickY]);
            ((IXbox360Controller)virtualController).SetAxisValue(Xbox360Axis.RightThumbX, Inputs.AxisState[AxisFlags.RightStickX]);
            ((IXbox360Controller)virtualController).SetAxisValue(Xbox360Axis.RightThumbY, Inputs.AxisState[AxisFlags.RightStickY]);

            ((IXbox360Controller)virtualController).SetSliderValue(Xbox360Slider.LeftTrigger, (byte)Inputs.AxisState[AxisFlags.L2]);
            ((IXbox360Controller)virtualController).SetSliderValue(Xbox360Slider.RightTrigger, (byte)Inputs.AxisState[AxisFlags.R2]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.A, Inputs.ButtonState[ButtonFlags.B1]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.B, Inputs.ButtonState[ButtonFlags.B2]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.X, Inputs.ButtonState[ButtonFlags.B3]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Y, Inputs.ButtonState[ButtonFlags.B4]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Up, Inputs.ButtonState[ButtonFlags.DPadUp]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Down, Inputs.ButtonState[ButtonFlags.DPadDown]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Left, Inputs.ButtonState[ButtonFlags.DPadLeft]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Right, Inputs.ButtonState[ButtonFlags.DPadRight]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Back, Inputs.ButtonState[ButtonFlags.Back]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Start, Inputs.ButtonState[ButtonFlags.Start]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.LeftShoulder, Inputs.ButtonState[ButtonFlags.L1]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.RightShoulder, Inputs.ButtonState[ButtonFlags.R1]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.LeftThumb, Inputs.ButtonState[ButtonFlags.LeftStickClick]);
            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.RightThumb, Inputs.ButtonState[ButtonFlags.RightStickClick]);

            ((IXbox360Controller)virtualController).SetButtonState(Xbox360Button.Guide, Inputs.ButtonState[ButtonFlags.Special]);

            try
            {
                virtualController.SubmitReport();
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
            if (virtualController is not null)
            {
                virtualController.Disconnect();
                virtualController = null;
            }

            base.Dispose();
        }
    }
}