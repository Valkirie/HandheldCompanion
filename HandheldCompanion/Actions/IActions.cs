using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Actions
{
    public enum ActionType
    {
        None = 0,
        Button = 1,
        Mouse = 2,
        Keyboard = 3,
        Axis = 4
    }

    [Serializable]
    public class IActions
    {
        public ActionType ActionType;

        public virtual void Execute(ButtonFlags button, bool value)
        { }

        public virtual void Execute(AxisFlags axis, short value)
        { }

        public virtual void Execute(object sender, bool value)
        { }

        public virtual void Execute(object sender, short value)
        { }
    }
}
