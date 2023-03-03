using ControllerCommon.Actions;
using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace ControllerCommon
{
    [Serializable]
    public class Layout
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<ButtonFlags, IActions> ButtonLayout { get; set; }
        public Dictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; }

        #region events
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Layout layout);
        #endregion

        public Layout()
        {
            this.ButtonLayout = new();
            this.AxisLayout = new();
        }

        public Layout(string name) : this()
        {
            this.Name = name;
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
    }
}
