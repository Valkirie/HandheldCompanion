using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;

namespace ControllerCommon
{
    [Serializable]
    public class Layout : ICloneable
    {
        public bool Enabled { get; set; } = false;

        public Dictionary<ButtonFlags, IActions> ButtonLayout { get; set; } = new();
        public Dictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; } = new();

        #region events
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Layout layout);
        #endregion

        public Layout() { }

        public Layout(string name) : this()
        {
            // generic button mapping
            foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
            {
                if (IController.VirtualButtons.Contains(button))
                    continue;

                ButtonLayout[button] = new ButtonActions() { Button = button };
            }

            ButtonLayout[ButtonFlags.OEM1] = new ButtonActions() { Button = ButtonFlags.Special };
            ButtonLayout[ButtonFlags.LeftPadClickUp] = new ButtonActions() { Button = ButtonFlags.DPadUp };
            ButtonLayout[ButtonFlags.LeftPadClickDown] = new ButtonActions() { Button = ButtonFlags.DPadDown };
            ButtonLayout[ButtonFlags.LeftPadClickLeft] = new ButtonActions() { Button = ButtonFlags.DPadLeft };
            ButtonLayout[ButtonFlags.LeftPadClickRight] = new ButtonActions() { Button = ButtonFlags.DPadRight };

            // generic axis mapping
            foreach (AxisLayoutFlags axis in Enum.GetValues(typeof(AxisLayoutFlags)))
            {
                if (IController.VirtualAxis.Contains(axis))
                    continue;

                AxisLayout[axis] = new AxisActions() { Axis = axis };
            }
        }

        public void UpdateLayout(ButtonFlags button, IActions action)
        {
            this.ButtonLayout[button] = action;
            Updated?.Invoke(this);
        }

        public void UpdateLayout(AxisLayoutFlags axis, IActions action)
        {
            this.AxisLayout[axis] = action;
            Updated?.Invoke(this);
        }

        public void RemoveLayout(ButtonFlags button)
        {
            this.ButtonLayout.Remove(button);
            Updated?.Invoke(this);
        }

        public void RemoveLayout(AxisLayoutFlags axis)
        {
            this.AxisLayout.Remove(axis);
            Updated?.Invoke(this);
        }

        // Improve me !
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
