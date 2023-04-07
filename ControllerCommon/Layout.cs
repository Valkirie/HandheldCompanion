using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ControllerCommon
{
    [Serializable]
    public class Layout : ICloneable, IDisposable
    {
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

                switch (axis)
                {
                    case AxisLayoutFlags.L2:
                    case AxisLayoutFlags.R2:
                        AxisLayout[axis] = new TriggerActions() { Axis = axis };
                        break;
                    default:
                        AxisLayout[axis] = new AxisActions() { Axis = axis };
                        break;
                }
            }
        }

        public void UpdateLayout()
        {
            Updated?.Invoke(this);
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

        public object Clone()
        {
            string jsonString = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            return JsonConvert.DeserializeObject<Layout>(jsonString, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        }

        public void Dispose()
        {
            ButtonLayout.Clear();
            AxisLayout.Clear();
        }
    }
}
