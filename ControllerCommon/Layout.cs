using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon
{
    [Serializable]
    public class Layout
    {
        public Dictionary<ButtonFlags, IActions> ButtonLayout { get; set; }
        public Dictionary<AxisFlags, IActions> AxisLayout { get; set; }

        public Layout()
        {
            this.ButtonLayout = new();
            this.AxisLayout = new();
        }
    }
}
