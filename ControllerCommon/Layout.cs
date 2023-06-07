﻿using System;
using System.Collections.Generic;
using ControllerCommon.Actions;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using Newtonsoft.Json;

namespace ControllerCommon;

[Serializable]
public class Layout : ICloneable, IDisposable
{
    public Layout()
    {
    }

    public Layout(bool fill) : this()
    {
        // generic button mapping
        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
        {
            if (!IController.TargetButtons.Contains(button))
                continue;

            ButtonLayout[button] = new ButtonActions { Button = button };
        }

        ButtonLayout[ButtonFlags.OEM1] = new ButtonActions { Button = ButtonFlags.Special };
        ButtonLayout[ButtonFlags.LeftPadClickUp] = new ButtonActions { Button = ButtonFlags.DPadUp };
        ButtonLayout[ButtonFlags.LeftPadClickDown] = new ButtonActions { Button = ButtonFlags.DPadDown };
        ButtonLayout[ButtonFlags.LeftPadClickLeft] = new ButtonActions { Button = ButtonFlags.DPadLeft };
        ButtonLayout[ButtonFlags.LeftPadClickRight] = new ButtonActions { Button = ButtonFlags.DPadRight };

        // generic axis mapping
        foreach (AxisLayoutFlags axis in Enum.GetValues(typeof(AxisLayoutFlags)))
        {
            if (!IController.TargetAxis.Contains(axis))
                continue;

            switch (axis)
            {
                case AxisLayoutFlags.L2:
                case AxisLayoutFlags.R2:
                    AxisLayout[axis] = new TriggerActions { Axis = axis };
                    break;
                default:
                    AxisLayout[axis] = new AxisActions { Axis = axis };
                    break;
            }
        }
    }

    public SortedDictionary<ButtonFlags, IActions> ButtonLayout { get; set; } = new();
    public SortedDictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; } = new();

    public object Clone()
    {
        var jsonString = JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        return JsonConvert.DeserializeObject<Layout>(jsonString,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
    }

    public void Dispose()
    {
        ButtonLayout.Clear();
        AxisLayout.Clear();
    }

    public void UpdateLayout()
    {
        Updated?.Invoke(this);
    }

    public void UpdateLayout(ButtonFlags button, IActions action)
    {
        ButtonLayout[button] = action;
        Updated?.Invoke(this);
    }

    public void UpdateLayout(AxisLayoutFlags axis, IActions action)
    {
        AxisLayout[axis] = action;
        Updated?.Invoke(this);
    }

    public void RemoveLayout(ButtonFlags button)
    {
        ButtonLayout.Remove(button);
        Updated?.Invoke(this);
    }

    public void RemoveLayout(AxisLayoutFlags axis)
    {
        AxisLayout.Remove(axis);
        Updated?.Invoke(this);
    }

    #region events

    public event UpdatedEventHandler Updated;

    public delegate void UpdatedEventHandler(Layout layout);

    #endregion
}