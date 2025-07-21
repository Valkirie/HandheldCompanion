using HandheldCompanion.Shared;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers.Zotac
{
    public class ZoneController : XInputController
    {
        private controller_hidapi.net.GenericController? Controller;
        private byte[] Data = new byte[64];

        public ZoneController() : base()
        { }

        public ZoneController(PnPDetails details) : base(details)
        { }

        public override void AttachDetails(PnPDetails details)
        {
            base.AttachDetails(details);

            // (un)plug controller if needed
            bool WasPlugged = Controller?.Reading == true && Controller?.IsDeviceValid == true;
            if (WasPlugged) Close();

            // create controller
            Controller = new(details.VendorID, details.ProductID, 64, 3);

            // (re)plug controller if needed
            if (WasPlugged) Open();
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
    }
}
