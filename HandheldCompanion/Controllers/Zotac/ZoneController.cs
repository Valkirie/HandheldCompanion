using HandheldCompanion.Devices;
using HandheldCompanion.Devices.Zotac;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using System;
using System.Timers;

namespace HandheldCompanion.Controllers.Zotac
{
    public class ZoneController : XInputController
    {
        private controller_hidapi.net.GenericController? Controller;
        private byte[] Data = new byte[64];

        private ButtonWheel pendingLeft = ButtonWheel.None;
        private Timer leftTimer = new Timer(100) { AutoReset = false };
        private ButtonWheel acceptedLeft = ButtonWheel.None;

        private ButtonWheel pendingRight = ButtonWheel.None;
        private Timer rightTimer = new Timer(100) { AutoReset = false };
        private ButtonWheel acceptedRight = ButtonWheel.None;

        [Flags]
        public enum ButtonWheel
        {
            None = 0,
            RightClock = 1,
            RightAnti = 2,
            Unk1 = 4,
            LeftClock = 8,
            LeftAnti = 16,
        }

        public ZoneController() : base()
        { }

        public ZoneController(PnPDetails details) : base(details)
        { }

        public override string ToString()
        {
            return "Zotac Controller";
        }

        protected override void InitializeInputOutput()
        {
            SourceButtons.Add(ButtonFlags.B5);  // left wheel counterclockwise
            SourceButtons.Add(ButtonFlags.B6);  // left wheel clockwise
            SourceButtons.Add(ButtonFlags.B7);  // right wheel counterclockwise
            SourceButtons.Add(ButtonFlags.B8);  // right wheel clockwise
        }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = Controller?.Reading == true && Controller?.IsDeviceValid == true;
            if (WasPlugged) Close();

            // create controller
            // interface: 0x01
            // length: 0x04
            Controller = new(details.VendorID, details.ProductID, 0x04, 0x01);

            // (re)plug controller if needed
            if (WasPlugged) Open();
        }

        public override void UpdateInputs(long ticks, float delta, bool commit)
        {
            // skip if controller isn't connected
            if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
                return;

            base.UpdateInputs(ticks, delta, false);

            ButtonWheel buttonWheel = acceptedLeft | acceptedRight;
            Inputs.ButtonState[ButtonFlags.B5] = buttonWheel.HasFlag(ButtonWheel.LeftAnti);
            Inputs.ButtonState[ButtonFlags.B6] = buttonWheel.HasFlag(ButtonWheel.LeftClock);
            Inputs.ButtonState[ButtonFlags.B7] = buttonWheel.HasFlag(ButtonWheel.RightAnti);
            Inputs.ButtonState[ButtonFlags.B8] = buttonWheel.HasFlag(ButtonWheel.RightClock);
            
            // clear wheel reading
            acceptedLeft = ButtonWheel.None;
            acceptedRight = ButtonWheel.None;

            base.UpdateInputs(ticks, delta);
        }

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

                        leftTimer.Elapsed += AcceptPendingLeft;
                        rightTimer.Elapsed += AcceptPendingRight;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(ZoneController), ex.Message);
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

                    leftTimer.Elapsed -= AcceptPendingLeft;
                    rightTimer.Elapsed -= AcceptPendingRight;
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

        public override void Unplug()
        {
            Close();
            base.Unplug();
        }

        public override bool CyclePort()
        {
            // set flag
            bool success = false;

            // set status
            IsBusy = true;
            ControllerManager.PowerCyclers[GetContainerInstanceId()] = true;

            // Looks like this will reboot the controller...
            if (IDevice.GetCurrent() is GamingZone gamingZone)
            {
                success = gamingZone.CycleController();
            }

            if (!success)
            {
                // (re)set status
                IsBusy = false;
                ControllerManager.PowerCyclers[GetContainerInstanceId()] = false;
            }

            return success;
        }

        private void Controller_OnControllerInputReceived(byte[] Data)
        {
            // byte[0] = 0
            // byte[1] = 0
            // byte[2] = 1 right clockwise | 2 right anticlockwise | 8 left clockwise | 16 left anticlockwise
            ButtonWheel wheelState = (ButtonWheel)Data[3];

            ButtonWheel leftInput = wheelState & (ButtonWheel.LeftClock | ButtonWheel.LeftAnti);
            if (leftInput != ButtonWheel.None)
            {
                pendingLeft = leftInput;
                leftTimer.Stop();
                leftTimer.Start();
            }
            else if (pendingLeft != ButtonWheel.None)
            {
                // Immediate commit on zero
                leftTimer.Stop();
                AcceptPendingLeft(null, null);
            }

            ButtonWheel rightInput = wheelState & (ButtonWheel.RightClock | ButtonWheel.RightAnti);
            if (rightInput != ButtonWheel.None)
            {
                pendingRight = rightInput;
                rightTimer.Stop();
                rightTimer.Start();
            }
            else if (pendingRight != ButtonWheel.None)
            {
                // Immediate commit on zero
                rightTimer.Stop();
                AcceptPendingRight(null, null);
            }

            // copy buffer without reportID
            Buffer.BlockCopy(Data, 1, this.Data, 0, Data.Length - 1);
        }

        private void AcceptPendingLeft(object? sender, ElapsedEventArgs e)
        {
            if (pendingLeft != ButtonWheel.None)
            {
                acceptedLeft = pendingLeft;
            }
            pendingLeft = ButtonWheel.None;
        }

        private void AcceptPendingRight(object? sender, ElapsedEventArgs e)
        {
            if (pendingRight != ButtonWheel.None)
            {
                acceptedRight = pendingRight;
            }
            pendingRight = ButtonWheel.None;
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.B5:
                    return "\u21AA";
                case ButtonFlags.B6:
                    return "\u21A9";
                case ButtonFlags.B7:
                    return "\u21AC";
                case ButtonFlags.B8:
                    return "\u21AB";
            }

            return base.GetGlyph(button);
        }
    }
}
