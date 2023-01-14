using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ControllerService.Targets
{
    internal partial class Xbox360Target : ViGEmTarget
    {
        private new IXbox360Controller virtualController;

        public Xbox360Target() : base()
        {
            // initialize controller
            HID = HIDmode.Xbox360Controller;

            virtualController = ControllerService.vClient.CreateXbox360Controller();
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;

            UpdateTimer.Tick += (sender, e) => UpdateReport();

            LogManager.LogInformation("{0} initialized, {1}", ToString(), virtualController);
        }

        public override void Connect()
        {
            if (IsConnected)
                return;

            try
            {
                virtualController.Connect();
                UpdateTimer.Start();

                base.Connect();
            }
            catch { }
        }

        public override void Disconnect()
        {
            if (!IsConnected)
                return;

            try
            {
                virtualController.Disconnect();
                UpdateTimer.Stop();

                base.Disconnect();
            }
            catch { }
        }

        public void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            // pass raw vibration to client
            PipeServer.SendMessage(new PipeClientVibration() { LargeMotor = e.LargeMotor, SmallMotor = e.SmallMotor });
        }

        public override unsafe void UpdateReport()
        {
            if (!IsConnected)
                return;

            if (IsSilenced)
                return;

            base.UpdateReport();

            virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, (short)LeftThumb.X);
            virtualController.SetAxisValue(Xbox360Axis.LeftThumbY, (short)LeftThumb.Y);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbX, (short)RightThumb.X);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbY, (short)RightThumb.Y);

            virtualController.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)LeftTrigger);
            virtualController.SetSliderValue(Xbox360Slider.RightTrigger, (byte)RightTrigger);

            virtualController.SetButtonState(Xbox360Button.A, Inputs.Buttons.HasFlag(ControllerButtonFlags.B1));
            virtualController.SetButtonState(Xbox360Button.B, Inputs.Buttons.HasFlag(ControllerButtonFlags.B2));
            virtualController.SetButtonState(Xbox360Button.X, Inputs.Buttons.HasFlag(ControllerButtonFlags.B3));
            virtualController.SetButtonState(Xbox360Button.Y, Inputs.Buttons.HasFlag(ControllerButtonFlags.B4));

            virtualController.SetButtonState(Xbox360Button.Left, Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadLeft));
            virtualController.SetButtonState(Xbox360Button.Right, Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadRight));
            virtualController.SetButtonState(Xbox360Button.Down, Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadDown));
            virtualController.SetButtonState(Xbox360Button.Up, Inputs.Buttons.HasFlag(ControllerButtonFlags.DPadUp));

            virtualController.SetButtonState(Xbox360Button.Back, Inputs.Buttons.HasFlag(ControllerButtonFlags.Back));
            virtualController.SetButtonState(Xbox360Button.Start, Inputs.Buttons.HasFlag(ControllerButtonFlags.Start));

            virtualController.SetButtonState(Xbox360Button.LeftShoulder, Inputs.Buttons.HasFlag(ControllerButtonFlags.LeftShoulder));
            virtualController.SetButtonState(Xbox360Button.RightShoulder, Inputs.Buttons.HasFlag(ControllerButtonFlags.RightShoulder));

            virtualController.SetButtonState(Xbox360Button.LeftThumb, Inputs.Buttons.HasFlag(ControllerButtonFlags.LeftThumb));
            virtualController.SetButtonState(Xbox360Button.RightThumb, Inputs.Buttons.HasFlag(ControllerButtonFlags.RightThumb));

            virtualController.SetButtonState(Xbox360Button.Guide, Inputs.Buttons.HasFlag(ControllerButtonFlags.Special));

            try
            {
                virtualController.SubmitReport();
            }
            catch (VigemBusNotFoundException ex)
            {
                LogManager.LogCritical(ex.Message);
            }

            base.SubmitReport();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
