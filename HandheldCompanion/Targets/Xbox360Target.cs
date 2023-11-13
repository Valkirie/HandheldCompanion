using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System;

namespace HandheldCompanion.Targets
{
    internal partial class Xbox360Target : ViGEmTarget
    {
        private new IXbox360Controller virtualController;

        public Xbox360Target() : base()
        {
            // initialize controller
            HID = HIDmode.Xbox360Controller;

            virtualController = VirtualManager.vClient.CreateXbox360Controller((ushort)new Random().Next(), (ushort)new Random().Next());
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public override void Connect()
        {
            if (IsConnected)
                return;

            try
            {
                virtualController.Connect();
                TimerManager.Tick += UpdateReport;

                base.Connect();
            }
            catch (Exception ex)
            {
                virtualController.Disconnect();
                LogManager.LogWarning("Failed to connect {0}. {1}", this.ToString(), ex.Message);
            }
        }

        public override void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                virtualController.Disconnect();
                TimerManager.Tick -= UpdateReport;

                base.Disconnect();
            }
            catch { }
        }

        public void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            SendVibrate(e.LargeMotor, e.SmallMotor);
        }

        public override unsafe void UpdateReport(long ticks)
        {
            if (!IsConnected)
                return;

            virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, Inputs.AxisState[AxisFlags.LeftStickX]);
            virtualController.SetAxisValue(Xbox360Axis.LeftThumbY, Inputs.AxisState[AxisFlags.LeftStickY]);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbX, Inputs.AxisState[AxisFlags.RightStickX]);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbY, Inputs.AxisState[AxisFlags.RightStickY]);

            virtualController.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)Inputs.AxisState[AxisFlags.L2]);
            virtualController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)Inputs.AxisState[AxisFlags.R2]);

            virtualController.SetButtonState(Xbox360Button.A, Inputs.ButtonState[ButtonFlags.B1]);
            virtualController.SetButtonState(Xbox360Button.B, Inputs.ButtonState[ButtonFlags.B2]);
            virtualController.SetButtonState(Xbox360Button.X, Inputs.ButtonState[ButtonFlags.B3]);
            virtualController.SetButtonState(Xbox360Button.Y, Inputs.ButtonState[ButtonFlags.B4]);

            virtualController.SetButtonState(Xbox360Button.Up, Inputs.ButtonState[ButtonFlags.DPadUp]);
            virtualController.SetButtonState(Xbox360Button.Down, Inputs.ButtonState[ButtonFlags.DPadDown]);
            virtualController.SetButtonState(Xbox360Button.Left, Inputs.ButtonState[ButtonFlags.DPadLeft]);
            virtualController.SetButtonState(Xbox360Button.Right, Inputs.ButtonState[ButtonFlags.DPadRight]);

            virtualController.SetButtonState(Xbox360Button.Back, Inputs.ButtonState[ButtonFlags.Back]);
            virtualController.SetButtonState(Xbox360Button.Start, Inputs.ButtonState[ButtonFlags.Start]);

            virtualController.SetButtonState(Xbox360Button.LeftShoulder, Inputs.ButtonState[ButtonFlags.L1]);
            virtualController.SetButtonState(Xbox360Button.RightShoulder, Inputs.ButtonState[ButtonFlags.R1]);

            virtualController.SetButtonState(Xbox360Button.LeftThumb, Inputs.ButtonState[ButtonFlags.LeftStickClick]);
            virtualController.SetButtonState(Xbox360Button.RightThumb, Inputs.ButtonState[ButtonFlags.RightStickClick]);

            virtualController.SetButtonState(Xbox360Button.Guide, Inputs.ButtonState[ButtonFlags.Special]);

            try
            {
                virtualController.SubmitReport();
            }
            catch (VigemBusNotFoundException ex)
            {
                LogManager.LogCritical(ex.Message);
            }
            catch (VigemInvalidTargetException ex)
            {
                LogManager.LogCritical(ex.Message);
            }
        }

        public override void Dispose()
        {
            if (virtualController is not null)
                virtualController.Disconnect();

            base.Dispose();
        }
    }
}