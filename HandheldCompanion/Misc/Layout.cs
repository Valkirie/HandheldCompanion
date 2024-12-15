using HandheldCompanion.Actions;
using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion;

[Serializable]
public partial class Layout : ICloneable, IDisposable
{
    public SortedDictionary<ButtonFlags, List<IActions>> ButtonLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, IActions> AxisLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, IActions> GyroLayout { get; set; } = [];

    public bool IsDefaultLayout { get; set; }

    public Layout()
    {
    }

    ~Layout()
    {
        Dispose();
    }

    public void FillInherit()
    {
        // Get the current controller
        IController controller = ControllerManager.GetDefaultXBOX();

        // Generic button mapping
        foreach (ButtonFlags button in controller.GetTargetButtons())
            ButtonLayout[button] = [new InheritActions()];

        // Generic axis mapping
        var allAxes = controller.GetTargetAxis().Union(controller.GetTargetTriggers());
        foreach (AxisLayoutFlags axis in allAxes)
            AxisLayout[axis] = new InheritActions();
    }

    public void FillDefault()
    {
        // Get the current controller
        IController controller = ControllerManager.GetDefaultXBOX();

        // Generic button mapping
        foreach (ButtonFlags button in controller.GetTargetButtons())
            ButtonLayout[button] = new List<IActions> { new ButtonActions { Button = button } };

        // Generic axis mappings
        foreach (AxisLayoutFlags axis in controller.GetTargetAxis())
            AxisLayout[axis] = new AxisActions { Axis = axis };

        // Trigger axis mappings
        foreach (AxisLayoutFlags axis in controller.GetTargetTriggers())
            AxisLayout[axis] = new TriggerActions { Axis = axis };

        // Special button mappings
        Dictionary<ButtonFlags, ButtonFlags> specialButtonMappings = new Dictionary<ButtonFlags, ButtonFlags>
        {
            { ButtonFlags.LeftPadClickUp, ButtonFlags.DPadUp },
            { ButtonFlags.LeftPadClickDown, ButtonFlags.DPadDown },
            { ButtonFlags.LeftPadClickLeft, ButtonFlags.DPadLeft },
            { ButtonFlags.LeftPadClickRight, ButtonFlags.DPadRight },
            { ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadTouch },
            { ButtonFlags.LeftPadClick, ButtonFlags.LeftPadClick },
            { ButtonFlags.RightPadTouch, ButtonFlags.RightPadTouch },
            { ButtonFlags.RightPadClick, ButtonFlags.RightPadClick }
        };

        foreach (KeyValuePair<ButtonFlags, ButtonFlags> mapping in specialButtonMappings)
            ButtonLayout[mapping.Key] = new List<IActions> { new ButtonActions { Button = mapping.Value } };

        // Add specific axis mappings
        AxisLayout[AxisLayoutFlags.LeftPad] = new AxisActions { Axis = AxisLayoutFlags.LeftPad };
        AxisLayout[AxisLayoutFlags.RightPad] = new AxisActions { Axis = AxisLayoutFlags.RightPad };
    }

    public object Clone()
    {
        // Clone shouldn't be default layout in case it is true
        Layout clone = CloningHelper.DeepClone(this);
        clone.IsDefaultLayout = false;

        return clone;
    }

    public void Dispose()
    {
        ButtonLayout.Clear();
        AxisLayout.Clear();
        GyroLayout.Clear();

        GC.SuppressFinalize(this);
    }

    public void UpdateLayout()
    {
        Updated?.Invoke(this);
    }

    public void UpdateLayout(ButtonFlags button, List<IActions> actions)
    {
        // sort actions based on press type, will be required by layout manager
        ButtonLayout[button] = actions.OrderByDescending(a => (int)a.pressType).ToList();
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