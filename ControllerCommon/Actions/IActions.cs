using ControllerCommon.Inputs;
using System;

namespace ControllerCommon.Actions
{
    [Serializable]
    public enum ActionType
    {
        None = 0,
        Button = 1,
        Axis = 2,
        Keyboard = 3,
        Mouse = 4
    }

    [Serializable]
    public abstract class IActions
    {
        public ActionType ActionType { get; set; }

        protected object Value;
        protected object prevValue;

        public bool Turbo { get; set; }
        public byte TurboDelay { get; set; } = 90;
        protected short TurboIdx;
        protected bool IsTurboed;

        public bool Toggle { get; set; }
        protected bool IsToggled;

        protected const short UPDATE_INTERVAL = 10;

        public virtual void Execute(ButtonFlags button, bool value)
        {
        }

        public virtual void Execute(ButtonFlags button, short value)
        {
        }

        public virtual void Execute(AxisFlags axis, bool value)
        {
        }

        public virtual void Execute(AxisFlags axis, short value)
        {
        }
    }
}
