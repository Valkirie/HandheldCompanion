using ControllerCommon.Inputs;
using System;

namespace ControllerCommon.Actions
{
    [Serializable]
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
        public ActionType ActionType { get; set; }

        public virtual void Execute(ButtonFlags button, bool value)
        { }

        public virtual void Execute(AxisFlags axis, bool value)
        { }

        public virtual void Execute(AxisFlags axis, short value)
        { }
    }
}
