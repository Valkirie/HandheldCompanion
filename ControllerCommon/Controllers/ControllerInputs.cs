using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace ControllerCommon.Controllers
{
    [Serializable]
    public class ControllerInputs
    {
        public ButtonState ButtonState = new();
        public AxisState AxisState = new();

        public int Timestamp;

        public ControllerInputs()
        { }

        public ControllerInputs(ControllerInputs Inputs)
        {
            ButtonState = Inputs.ButtonState;
            AxisState = Inputs.AxisState;

            Timestamp = Inputs.Timestamp;
        }
    }
}
