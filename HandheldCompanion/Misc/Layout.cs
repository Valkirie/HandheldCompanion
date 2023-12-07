using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HandheldCompanion;

[Serializable]
public partial class Layout : ICloneable, IDisposable
{
    public SortedDictionary<ButtonFlags, List<IActions>> ButtonLayout { get; set; } = new();
    public SortedDictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; } = new();
    public SortedDictionary<AxisLayoutFlags, IActions> GyroLayout { get; set; } = new();

    public bool IsDefaultLayout { get; set; }

    // gyro related

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

            ButtonLayout[button] = new List<IActions>() { new ButtonActions() { Button = button } };
        }

        // ButtonLayout[ButtonFlags.OEM1] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.Special } };
        ButtonLayout[ButtonFlags.LeftPadClickUp] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.DPadUp } };
        ButtonLayout[ButtonFlags.LeftPadClickDown] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.DPadDown } };
        ButtonLayout[ButtonFlags.LeftPadClickLeft] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.DPadLeft } };
        ButtonLayout[ButtonFlags.LeftPadClickRight] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.DPadRight } };

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

    public object Clone()
    {
        var jsonString = JsonConvert.SerializeObject(this, Formatting.Indented,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
        var deserialized = JsonConvert.DeserializeObject<Layout>(jsonString,
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

        deserialized.IsDefaultLayout = false; // Clone shouldn't be default layout in case it is true
        
        return deserialized;
    }

    public void Dispose()
    {
        ButtonLayout.Clear();
        AxisLayout.Clear();
        GyroLayout.Clear();
    }

    public void UpdateLayout()
    {
        Updated?.Invoke(this);
    }

    public void UpdateLayout(ButtonFlags button, List<IActions> actions)
    {
        ButtonLayout[button] = actions;
        Updated?.Invoke(this);
    }

    public void UpdateLayout(AxisLayoutFlags axis, IActions action)
    {
        switch(axis)
        {
            default:
                AxisLayout[axis] = action;
                break;
            case AxisLayoutFlags.Gyroscope:
                GyroLayout[axis] = action;
                break;
        }
        Updated?.Invoke(this);
    }

    public void RemoveLayout(ButtonFlags button)
    {
        ButtonLayout.Remove(button);
        Updated?.Invoke(this);
    }

    public void RemoveLayout(AxisLayoutFlags axis)
    {
        switch (axis)
        {
            default:
                AxisLayout.Remove(axis);
                break;
            case AxisLayoutFlags.Gyroscope:
                GyroLayout.Remove(axis);
                break;
        }
        Updated?.Invoke(this);
    }

    #region events
    public event UpdatedEventHandler Updated;
    public delegate void UpdatedEventHandler(Layout layout);
    #endregion
}