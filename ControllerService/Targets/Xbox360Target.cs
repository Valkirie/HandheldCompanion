using ControllerCommon.Utils;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System.Collections.Generic;
using GamepadButtonFlags = SharpDX.XInput.GamepadButtonFlags;

namespace ControllerService.Targets
{
    internal partial class Xbox360Target : ViGEmTarget
    {
        private static readonly List<Xbox360Button> ButtonMap = new List<Xbox360Button>
        {
            Xbox360Button.Up,
            Xbox360Button.Down,
            Xbox360Button.Left,
            Xbox360Button.Right,
            Xbox360Button.Start,
            Xbox360Button.Back,
            Xbox360Button.LeftThumb,
            Xbox360Button.RightThumb,
            Xbox360Button.LeftShoulder,
            Xbox360Button.RightShoulder,
            Xbox360Button.Guide,
            Xbox360Button.A,
            Xbox360Button.B,
            Xbox360Button.X,
            Xbox360Button.Y
        };

        private static readonly List<Xbox360Axis> AxisMap = new List<Xbox360Axis>
        {
            Xbox360Axis.LeftThumbX,
            Xbox360Axis.LeftThumbY,
            Xbox360Axis.RightThumbX,
            Xbox360Axis.RightThumbY
        };

        private static readonly List<Xbox360Slider> SliderMap = new List<Xbox360Slider>
        {
            Xbox360Slider.LeftTrigger,
            Xbox360Slider.RightTrigger
        };

        private new IXbox360Controller virtualController;

        public Xbox360Target(XInputController xinput, ViGEmClient client, ILogger logger) : base(xinput, client, logger)
        {
            // initialize controller
            HID = HIDmode.Xbox360Controller;

            virtualController = client.CreateXbox360Controller();
            virtualController.AutoSubmitReport = false;
            virtualController.FeedbackReceived += FeedbackReceived;
        }

        public override void Connect()
        {
            if (IsConnected)
                return;

            virtualController.Connect();
            base.Connect();
        }

        public override void Disconnect()
        {
            if (!IsConnected)
                return;

            virtualController.Disconnect();
            base.Disconnect();
        }

        public void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            if (!physicalController.IsConnected)
                return;

            Vibration inputMotor = new()
            {
                LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * vibrationStrength),
                RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * vibrationStrength),
            };
            physicalController.SetVibration(inputMotor);
        }

        public override unsafe void UpdateReport(Gamepad Gamepad)
        {
            if (!IsConnected)
                return;

            if (ControllerService.profile.whitelisted)
                return;

            base.UpdateReport(Gamepad);

            virtualController.SetAxisValue(Xbox360Axis.LeftThumbX, (short)LeftThumb.X);
            virtualController.SetAxisValue(Xbox360Axis.LeftThumbY, (short)LeftThumb.Y);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbX, (short)RightThumb.X);
            virtualController.SetAxisValue(Xbox360Axis.RightThumbY, (short)RightThumb.Y);

            foreach (Xbox360Button button in ButtonMap)
            {
                GamepadButtonFlags value = (GamepadButtonFlags)button.Value;
                virtualController.SetButtonState(button, Gamepad.Buttons.HasFlag(value));
            }

            virtualController.SetSliderValue(Xbox360Slider.LeftTrigger, Gamepad.LeftTrigger);
            virtualController.SetSliderValue(Xbox360Slider.RightTrigger, Gamepad.RightTrigger);

            virtualController.SubmitReport();

            base.SubmitReport();
        }
    }
}
