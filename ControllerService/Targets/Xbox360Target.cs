using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

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

        private new IXbox360Controller vcontroller;

        public Xbox360Target(ViGEmClient client, Controller controller, int index, ILogger logger) : base(client, controller, index, logger)
        {
            // initialize controller
            HID = HIDmode.Xbox360Controller;

            vcontroller = client.CreateXbox360Controller();
            vcontroller.AutoSubmitReport = false;
            vcontroller.FeedbackReceived += FeedbackReceived;
        }

        public new void Connect()
        {
            vcontroller.Connect();
            base.Connect();
        }

        public new void Disconnect()
        {
            vcontroller.Disconnect();
            base.Disconnect();
        }

        public void FeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            if (!Controller.IsConnected)
                return;

            Vibration inputMotor = new()
            {
                LeftMotorSpeed = (ushort)((e.LargeMotor * ushort.MaxValue / byte.MaxValue) * strength),
                RightMotorSpeed = (ushort)((e.SmallMotor * ushort.MaxValue / byte.MaxValue) * strength),
            };
            Controller.SetVibration(inputMotor);
        }

        public new unsafe void UpdateReport()
        {
            if (!Controller.IsConnected)
                return;

            base.UpdateReport();

            vcontroller.SetAxisValue(Xbox360Axis.LeftThumbX, LeftThumbX);
            vcontroller.SetAxisValue(Xbox360Axis.LeftThumbY, LeftThumbY);
            vcontroller.SetAxisValue(Xbox360Axis.RightThumbX, RightThumbX);
            vcontroller.SetAxisValue(Xbox360Axis.RightThumbY, RightThumbY);

            foreach (Xbox360Button button in ButtonMap)
            {
                GamepadButtonFlags value = (GamepadButtonFlags)button.Value;
                vcontroller.SetButtonState(button, Gamepad.Buttons.HasFlag(value));
            }

            vcontroller.SetSliderValue(Xbox360Slider.LeftTrigger, Gamepad.LeftTrigger);
            vcontroller.SetSliderValue(Xbox360Slider.RightTrigger, Gamepad.RightTrigger);

            if (!Profile.whitelisted)
                vcontroller.SubmitReport();
        }
    }
}
