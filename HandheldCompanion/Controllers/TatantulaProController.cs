using controller_hidapi.net;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System;

namespace HandheldCompanion.Controllers
{
    public class TatantulaProController : XInputController
    {
        private controller_hidapi.net.TarantulaProController? Controller;
        private byte[] Data = new byte[64];

        protected float aX = 0.0f, aZ = 0.0f, aY = 0.0f;
        protected float gX = 0.0f, gZ = 0.0f, gY = 0.0f;
        private const byte EXTRABUTTON0_IDX = 11;
        private const byte EXTRABUTTON1_IDX = 12;
        private const byte EXTRABUTTON2_IDX = 13;

        [Flags]
        private enum Button0Enum
        {
            None = 0,
            M = 4
        }

        [Flags]
        private enum Button1Enum
        {
            None = 0,
            M1 = 1,
            M2 = 2,
            T1 = 4,
            T2 = 8,
            T3 = 16,
            C1 = 32,
            C2 = 64,
            C3 = 128
        }

        [Flags]
        private enum Button2Enum
        {
            None = 0,
            C4 = 1
        }

        [Flags]
        private enum ButtonLayout
        {
            Xbox = 64,
            Nintendo = 128,
        }

        public TatantulaProController() : base()
        { }

        public TatantulaProController(PnPDetails details) : base(details)
        {
            // Capabilities
            Capabilities |= ControllerCapabilities.MotionSensor;
        }

        public override string ToString()
        {
            return $"GameSir Tarantula Pro PC Controller";
        }

        protected override void InitializeInputOutput()
        {
            SourceButtons.Add(ButtonFlags.R4);
            SourceButtons.Add(ButtonFlags.L4);
            SourceButtons.Add(ButtonFlags.L5);

            SourceButtons.Add(ButtonFlags.B5);
            SourceButtons.Add(ButtonFlags.B6);
            SourceButtons.Add(ButtonFlags.B7);
            SourceButtons.Add(ButtonFlags.B8);
            SourceButtons.Add(ButtonFlags.B9);
            SourceButtons.Add(ButtonFlags.B10);
            SourceButtons.Add(ButtonFlags.B11);

            SourceAxis.Add(AxisLayoutFlags.Gyroscope);
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = Controller?.Reading == true && Controller?.IsDeviceValid == true;
            if (WasPlugged) Close();

            // create controller
            Controller = new(details.VendorID, details.ProductID);

            // (re)plug controller if needed
            if (WasPlugged) Open();
        }

        private ButtonLayout GetLayout()
        {
            ButtonLayout layout = (ButtonLayout)Data[EXTRABUTTON2_IDX];
            return layout.HasFlag(ButtonLayout.Xbox) ? ButtonLayout.Xbox : ButtonLayout.Nintendo;
        }

        /*
        public override void Hide(bool powerCycle = true)
        {
            lock (hidLock)
            {
                Close();
                base.Hide(powerCycle);
                Open();
            }
        }

        public override void Unhide(bool powerCycle = true)
        {
            lock (hidLock)
            {
                Close();
                base.Unhide(powerCycle);
                Open();
            }
        }
        */

        private void Open()
        {
            lock (hidLock)
            {
                try
                {
                    // open controller
                    if (Controller is not null)
                    {
                        Controller.OnControllerInputReceived += Controller_OnControllerInputReceived;
                        Controller.Open();
                        Controller.SetTestMode();
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(TarantulaProController), ex.Message);
                    return;
                }
            }
        }

        private void Close()
        {
            lock (hidLock)
            {
                // close controller
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.Close();
                }
            }
        }

        public override void Gone()
        {
            lock (hidLock)
            {
                if (Controller is not null)
                {
                    Controller.OnControllerInputReceived -= Controller_OnControllerInputReceived;
                    Controller.EndRead();
                    Controller = null;
                }
            }
        }

        public override void Plug()
        {
            Open();
            base.Plug();
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            Buffer.BlockCopy(Data, 1, this.Data, 0, Data.Length - 1);
        }

        public override void Unplug()
        {
            Close();
            base.Unplug();
        }

        public override void SetLightColor(byte R, byte G, byte B)
        {
            Controller?.SetLightColor(R, G, B);
        }

        public override void UpdateInputs(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            base.UpdateInputs(ticks, delta, false);

            // TODO: Move me to controller-hidapi
            Button0Enum extraButtons0 = (Button0Enum)Data[EXTRABUTTON0_IDX];
            Inputs.ButtonState[ButtonFlags.L5] = extraButtons0.HasFlag(Button0Enum.M);

            Button1Enum extraButtons1 = (Button1Enum)Data[EXTRABUTTON1_IDX];
            Inputs.ButtonState[ButtonFlags.L4] = extraButtons1.HasFlag(Button1Enum.M1);
            Inputs.ButtonState[ButtonFlags.R4] = extraButtons1.HasFlag(Button1Enum.M2);

            Button2Enum extraButtons2 = (Button2Enum)Data[EXTRABUTTON2_IDX];
            Inputs.ButtonState[ButtonFlags.B5] = extraButtons1.HasFlag(Button1Enum.C1);
            Inputs.ButtonState[ButtonFlags.B6] = extraButtons1.HasFlag(Button1Enum.C2);
            Inputs.ButtonState[ButtonFlags.B7] = extraButtons1.HasFlag(Button1Enum.C3);
            Inputs.ButtonState[ButtonFlags.B8] = extraButtons2.HasFlag(Button2Enum.C4);

            Inputs.ButtonState[ButtonFlags.B9] = extraButtons1.HasFlag(Button1Enum.T1);
            Inputs.ButtonState[ButtonFlags.B10] = extraButtons1.HasFlag(Button1Enum.T2);
            Inputs.ButtonState[ButtonFlags.B11] = extraButtons1.HasFlag(Button1Enum.T3);

            aX = (short)(Data[14] << 8 | Data[15]) * (4.0f / short.MaxValue);
            aZ = (short)(Data[16] << 8 | Data[17]) * -(4.0f / short.MaxValue);
            aY = (short)(Data[18] << 8 | Data[19]) * (4.0f / short.MaxValue);

            gX = (short)(Data[20] << 8 | Data[21]) * (2000.0f / short.MaxValue);
            gZ = (short)(Data[22] << 8 | Data[23]) * -(2000.0f / short.MaxValue);
            gY = (short)(Data[24] << 8 | Data[25]) * (2000.0f / short.MaxValue);

            // compute motion from controller
            if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
                gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);

            Inputs.GyroState.SetGyroscope(gX, gY, gZ);
            Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

            base.UpdateInputs(ticks, delta);
        }

        public override string GetGlyph(ButtonFlags button)
        {
            return base.GetGlyph(button);
        }
    }
}