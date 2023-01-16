using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace ControllerCommon.Controllers
{
    [Serializable]
    public class ControllerState
    {
        public ButtonState ButtonState = new();
        public AxisState AxisState = new();

        public int Timestamp;

        public ControllerState()
        { }

        public ControllerState(ControllerState Inputs)
        {
            ButtonState = Inputs.ButtonState;
            AxisState = Inputs.AxisState;

            Timestamp = Inputs.Timestamp;
        }
    }
}
