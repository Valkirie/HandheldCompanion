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
    public abstract class IActions
    {
        public ActionType ActionType { get; set; }

        public virtual bool Execute(ButtonFlags button, bool value)
        {
            return value;
        }

        public virtual bool Execute(AxisFlags axis, bool value)
        {
            return value;
        }

        public virtual short Execute(AxisFlags axis, short value)
        {
            return value;
        }
    }
}
