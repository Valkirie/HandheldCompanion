using ControllerCommon;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
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
    internal class DualShock4Target : ViGEmTarget
    {
        private static readonly List<DualShock4Button> ButtonMap = new List<DualShock4Button>
        {
            DualShock4Button.ThumbRight,
            DualShock4Button.ThumbLeft,
            DualShock4Button.Options,
            DualShock4Button.Share,
            DualShock4Button.TriggerRight,
            DualShock4Button.TriggerLeft,
            DualShock4Button.ShoulderRight,
            DualShock4Button.ShoulderLeft,
            DualShock4Button.Triangle,
            DualShock4Button.Circle,
            DualShock4Button.Cross,
            DualShock4Button.Square,
            DualShock4SpecialButton.Ps,
            DualShock4SpecialButton.Touchpad
        };

        private static readonly List<DualShock4Axis> AxisMap = new List<DualShock4Axis>
        {
            DualShock4Axis.LeftThumbX,
            DualShock4Axis.LeftThumbY,
            DualShock4Axis.RightThumbX,
            DualShock4Axis.RightThumbY
        };

        private static readonly List<DualShock4Slider> SliderMap = new List<DualShock4Slider>
        {
            DualShock4Slider.LeftTrigger,
            DualShock4Slider.RightTrigger
        };

        private DS4_REPORT_EX outDS4Report;

        private const float F_ACC_RES_PER_G = 8192.0f;
        private const float F_GYRO_RES_IN_DEG_SEC = 16.0f;

        private new IDualShock4Controller vcontroller;

        public DualShock4Target(ViGEmClient client, Controller controller, int index) : base(client, controller, index)
        {
            // initialize controller
            vcontroller = client.CreateDualShock4Controller();
            vcontroller.AutoSubmitReport = false;

            vcontroller.Connect();
        }

        public new void Disconnect()
        {
            vcontroller.Disconnect();
        }

        public new unsafe void UpdateReport()
        {
            if (!Controller.IsConnected)
                return;

            base.UpdateReport();

            // reset vars
            byte[] rawOutReportEx = new byte[63];
            ushort tempButtons = 0;
            ushort tempSpecial = 0;
            DualShock4DPadDirection tempDPad = DualShock4DPadDirection.None;

            outDS4Report.bThumbLX = 127;
            outDS4Report.bThumbLY = 127;
            outDS4Report.bThumbRX = 127;
            outDS4Report.bThumbRY = 127;

            unchecked
            {
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                    tempButtons |= DualShock4Button.Cross.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                    tempButtons |= DualShock4Button.Circle.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                    tempButtons |= DualShock4Button.Square.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                    tempButtons |= DualShock4Button.Triangle.Value;

                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                    tempButtons |= DualShock4Button.Options.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                    tempButtons |= DualShock4Button.Share.Value;

                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                    tempButtons |= DualShock4Button.ThumbRight.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                    tempButtons |= DualShock4Button.ThumbLeft.Value;

                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                    tempButtons |= DualShock4Button.ShoulderRight.Value;
                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                    tempButtons |= DualShock4Button.ShoulderLeft.Value;

                if (Gamepad.LeftTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerLeft.Value;
                if (Gamepad.RightTrigger > 0)
                    tempButtons |= DualShock4Button.TriggerRight.Value;

                if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) &&
                    Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                    tempDPad = DualShock4DPadDirection.Northeast;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp) &&
                         Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Northwest;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                    tempDPad = DualShock4DPadDirection.North;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight) &&
                         Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                    tempDPad = DualShock4DPadDirection.Southeast;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                    tempDPad = DualShock4DPadDirection.East;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown) &&
                         Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.Southwest;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                    tempDPad = DualShock4DPadDirection.South;
                else if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                    tempDPad = DualShock4DPadDirection.West;

                if ((state_s.wButtons & 0x0400) == 0x0400)
                    tempSpecial |= DualShock4SpecialButton.Ps.Value;
                if (touch.OutputClickButton)
                    tempSpecial |= DualShock4SpecialButton.Touchpad.Value;

                outDS4Report.bSpecial = (byte)(tempSpecial | (0 << 2));
            }

            if (!profile.whitelisted)
            {
                outDS4Report.wButtons = tempButtons;
                outDS4Report.wButtons |= tempDPad.Value;

                outDS4Report.bTriggerL = Gamepad.LeftTrigger;
                outDS4Report.bTriggerR = Gamepad.RightTrigger;

                outDS4Report.bThumbLX = Utils.NormalizeInput(LeftThumbX);
                outDS4Report.bThumbLY = (byte)(byte.MaxValue - Utils.NormalizeInput(LeftThumbY));
                outDS4Report.bThumbRX = Utils.NormalizeInput(RightThumbX);
                outDS4Report.bThumbRY = (byte)(byte.MaxValue - Utils.NormalizeInput(RightThumbY));
            }

            unchecked
            {
                outDS4Report.bTouchPacketsN = 0x01;
                outDS4Report.sCurrentTouch.bPacketCounter = touch.TouchPacketCounter;
                outDS4Report.sCurrentTouch.bIsUpTrackingNum1 = touch.TrackPadTouch0.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData1[0] = (byte)(touch.TrackPadTouch0.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData1[1] =
                    (byte)(((touch.TrackPadTouch0.X >> 8) & 0x0F) | ((touch.TrackPadTouch0.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData1[2] = (byte)(touch.TrackPadTouch0.Y >> 4);

                outDS4Report.sCurrentTouch.bIsUpTrackingNum2 = touch.TrackPadTouch1.RawTrackingNum;
                outDS4Report.sCurrentTouch.bTouchData2[0] = (byte)(touch.TrackPadTouch1.X & 0xFF);
                outDS4Report.sCurrentTouch.bTouchData2[1] =
                    (byte)(((touch.TrackPadTouch1.X >> 8) & 0x0F) | ((touch.TrackPadTouch1.Y << 4) & 0xF0));
                outDS4Report.sCurrentTouch.bTouchData2[2] = (byte)(touch.TrackPadTouch1.Y >> 4);
            }

            outDS4Report.wGyroX = (short)(AngularVelocity.X * F_GYRO_RES_IN_DEG_SEC); // gyroPitchFull
            outDS4Report.wGyroY = (short)(-AngularVelocity.Y * F_GYRO_RES_IN_DEG_SEC); // gyroYawFull
            outDS4Report.wGyroZ = (short)(AngularVelocity.Z * F_GYRO_RES_IN_DEG_SEC); // gyroRollFull

            outDS4Report.wAccelX = (short)(-Acceleration.X * F_ACC_RES_PER_G); // accelXFull
            outDS4Report.wAccelY = (short)(-Acceleration.Y * F_ACC_RES_PER_G); // accelYFull
            outDS4Report.wAccelZ = (short)(Acceleration.Z * F_ACC_RES_PER_G); // accelZFull

            // digest previous value
            // AngularVelocity = new();
            // Acceleration = new();

            outDS4Report.bBatteryLvlSpecial = 11;

            outDS4Report.wTimestamp = (ushort)(microseconds / 5.33f);

            DS4OutDeviceExtras.CopyBytes(ref outDS4Report, rawOutReportEx);
            vcontroller.SubmitRawReport(rawOutReportEx);
        }
    }
}
