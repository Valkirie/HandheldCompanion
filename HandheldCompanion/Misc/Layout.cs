using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HandheldCompanion;

[Serializable]
public partial class Layout : ICloneable, IDisposable
{
    public SortedDictionary<ButtonFlags, List<IActions>> ButtonLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, IActions> GyroLayout { get; set; } = [];

    public bool IsDefaultLayout { get; set; }

    // gyro related

    public Layout()
    {
    }

    public Layout(bool fill) : this()
    {
        // reset layout(s)
        Dispose();

        // get current controller
        IController controller = ControllerManager.GetEmulatedController();

        // generic button mapping
        foreach (ButtonFlags button in Enum.GetValues(typeof(ButtonFlags)))
        {
            if (!controller.GetTargetButtons().Contains(button))
                continue;

            ButtonLayout[button] = [new ButtonActions() { Button = button }];
        }

        // ButtonLayout[ButtonFlags.OEM1] = new List<IActions>() { new ButtonActions { Button = ButtonFlags.Special } };
        ButtonLayout[ButtonFlags.LeftPadClickUp] = [new ButtonActions { Button = ButtonFlags.DPadUp }];
        ButtonLayout[ButtonFlags.LeftPadClickDown] = [new ButtonActions { Button = ButtonFlags.DPadDown }];
        ButtonLayout[ButtonFlags.LeftPadClickLeft] = [new ButtonActions { Button = ButtonFlags.DPadLeft }];
        ButtonLayout[ButtonFlags.LeftPadClickRight] = [new ButtonActions { Button = ButtonFlags.DPadRight }];

        // DualShock4
        ButtonLayout[ButtonFlags.LeftPadTouch] = [new ButtonActions { Button = ButtonFlags.LeftPadTouch }];
        ButtonLayout[ButtonFlags.LeftPadClick] = [new ButtonActions { Button = ButtonFlags.LeftPadClick }];
        ButtonLayout[ButtonFlags.RightPadTouch] = [new ButtonActions { Button = ButtonFlags.RightPadTouch }];
        ButtonLayout[ButtonFlags.RightPadClick] = [new ButtonActions { Button = ButtonFlags.RightPadClick }];

        // generic axis mapping
        foreach (AxisLayoutFlags axis in Enum.GetValues(typeof(AxisLayoutFlags)))
        {
            if (!controller.GetTargetAxis().Contains(axis))
                continue;

            AxisLayout[axis] = new AxisActions { Axis = axis };
        }

        AxisLayout[AxisLayoutFlags.LeftPad] = new AxisActions { Axis = AxisLayoutFlags.LeftPad };
        AxisLayout[AxisLayoutFlags.RightPad] = new AxisActions { Axis = AxisLayoutFlags.RightPad };

        // generic axis mapping
        foreach (AxisLayoutFlags axis in Enum.GetValues(typeof(AxisLayoutFlags)))
        {
            if (!controller.GetTargetTriggers().Contains(axis))
                continue;

            AxisLayout[axis] = new TriggerActions { Axis = axis };
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
        switch (axis)
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