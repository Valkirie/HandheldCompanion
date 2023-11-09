using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HidLibrary;
using SharpDX.XInput;
using steam_hidapi.net;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsInput.Native;

namespace HandheldCompanion.Controllers
{
    public class LegionController : XInputController
    {
        [Flags]
        private enum FrontEnum
        {
            None = 0,
            LegionR = 64,
            LegionL = 128,
        }

        [Flags]
        private enum BackEnum
        {
            None = 0,
            M3 = 4,
            M2 = 8,
            Y3 = 32,
            Y2 = 64,
            Y1 = 128,
        }

        private HidDevice hidDevice;
        private const byte FRONT_ID = 17;
        private const byte BACK_ID = 19;

        private byte[] Data = new byte[64];
        private bool updateLock = false;

        public LegionController(Controller controller, PnPDetails details) : base(controller, details)
        {
            // Additional controller specific source buttons
            SourceButtons.Add(ButtonFlags.RightPadTouch);
            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.R5);
            SourceButtons.Add(ButtonFlags.L4);
            SourceButtons.Add(ButtonFlags.L5);
            SourceButtons.Add(ButtonFlags.B5);
            SourceButtons.Add(ButtonFlags.B6);
            SourceButtons.Add(ButtonFlags.B7);
            SourceButtons.Add(ButtonFlags.B8);

            SourceAxis.Add(AxisLayoutFlags.RightPad);
            SourceAxis.Add(AxisLayoutFlags.Gyroscope);

            LegionGo legionDevice = MainWindow.CurrentDevice as LegionGo;
            hidDevice = legionDevice.hidDevices[LegionGo.INPUT_HID_ID];
        }

        public override void Plug()
        {
            hidDevice.OpenDevice();
            hidDevice.MonitorDeviceEvents = true;

            Task<HidReport> ReportDevice = Task.Run(async () => await hidDevice.ReadReportAsync());
            ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, hidDevice));

            base.Plug();
        }

        public override void Unplug()
        {
            hidDevice.CloseDevice();

            base.Unplug();
        }

        public override void UpdateInputs(long ticks, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            base.UpdateInputs(ticks, false);

            FrontEnum frontButton = (FrontEnum)Data[FRONT_ID];
            Inputs.ButtonState[ButtonFlags.OEM1] = frontButton.HasFlag(FrontEnum.LegionR);
            Inputs.ButtonState[ButtonFlags.OEM2] = frontButton.HasFlag(FrontEnum.LegionL);

            BackEnum backButton = (BackEnum)Data[BACK_ID];
            Inputs.ButtonState[ButtonFlags.R4] = backButton.HasFlag(BackEnum.M3);
            Inputs.ButtonState[ButtonFlags.R5] = backButton.HasFlag(BackEnum.Y3);
            Inputs.ButtonState[ButtonFlags.L4] = backButton.HasFlag(BackEnum.Y1);
            Inputs.ButtonState[ButtonFlags.L5] = backButton.HasFlag(BackEnum.Y2);

            Inputs.ButtonState[ButtonFlags.B5] = backButton.HasFlag(BackEnum.M2);
            Inputs.ButtonState[ButtonFlags.B6] = Data[20] == 128;   // Scroll click
            Inputs.ButtonState[ButtonFlags.B7] = Data[24] == 129;   // Scroll up
            Inputs.ButtonState[ButtonFlags.B8] = Data[24] == 255;   // Scroll down

            // Right Pad
            ushort TouchpadX = (ushort)((Data[25] << 8) | Data[26]);
            ushort TouchpadY = (ushort)((Data[27] << 8) | Data[28]);

            Inputs.ButtonState[ButtonFlags.RightPadTouch] = (TouchpadX != 0 || TouchpadY != 0);
            if (Inputs.ButtonState[ButtonFlags.RightPadTouch])
            {
                Inputs.AxisState[AxisFlags.RightPadX] = (short)InputUtils.MapRange((short)TouchpadX, 0, 1000, short.MinValue, short.MaxValue);
                Inputs.AxisState[AxisFlags.RightPadY] = (short)InputUtils.MapRange((short)-TouchpadY, 0, 1000, short.MinValue, short.MaxValue);
            }
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
            }

            // release lock
            updateLock = false;

            base.UpdateInputs(ticks);
        }

        private void OnReport(HidReport result, HidDevice device)
        {
            // wait until Data has been digested
            if (!updateLock)
            {
                // update data
                Data = result.Data;

                // set lock
                updateLock = true;
            }

            Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
            ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));
        }
    }
}
