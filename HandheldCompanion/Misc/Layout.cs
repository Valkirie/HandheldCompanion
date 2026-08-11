using HandheldCompanion.Actions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion;

[Serializable]
public partial class Layout : ICloneable, IDisposable
{
    public SortedDictionary<ButtonFlags, List<IActions>> ButtonLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, List<IActions>> AxisLayout { get; set; } = [];
    public SortedDictionary<AxisLayoutFlags, IActions> GyroLayout { get; set; } = [];

    [JsonIgnore]
    private readonly object _sync = new();
    public object SyncRoot => _sync;

    public Layout()
    {
    }

    ~Layout()
    {
        Dispose();
    }

    public void FillInherit()
    {
        // Generic and device button mapping
        foreach (ButtonFlags button in ButtonState.AllButtons)
        {
            if (ButtonState.UIButtons.Contains(button))
                continue;

            ButtonLayout[button] = [new InheritActions()];
        }

        // Generic axis mapping
        foreach (AxisLayoutFlags axis in AxisState.AllAxisLayoutFlags)
        {
            switch (axis)
            {
                default:
                    AxisLayout[axis] = [new InheritActions()];
                    break;
                case AxisLayoutFlags.Gyroscope:
                    // GyroLayout[axis] = new InheritActions();
                    break;
            }
        }
    }

    public void FillDefault()
    {
        // Generic button mapping
        foreach (ButtonFlags button in ButtonState.AllButtons)
        {
            if (ButtonState.UIButtons.Contains(button) || ButtonState.OEMButtons.Contains(button))
                continue;

            ButtonLayout[button] = button switch
            {
                ButtonFlags.LeftPadTouch => [new TouchpadActions(ButtonFlags.TouchpadTouch) { Finger = 1 }],
                ButtonFlags.RightPadTouch => [new TouchpadActions(ButtonFlags.TouchpadTouch) { Finger = 2 }],
                ButtonFlags.LeftPadClick => [new TouchpadActions(ButtonFlags.TouchpadClick) { Finger = 1 }],
                ButtonFlags.RightPadClick => [new TouchpadActions(ButtonFlags.TouchpadClick) { Finger = 2 }],
                _ when TouchpadActions.IsTouchpadButton(button) => [new TouchpadActions(button)],
                _ => [new ButtonActions { Button = button }],
            };
        }

        // Generic axis mappings
        foreach (AxisLayoutFlags axis in AxisState.AllAxisLayoutFlags)
        {
            switch (axis)
            {
                default:
                    AxisLayout[axis] = TouchpadActions.IsTouchpadAxis(axis)
                        ? [new TouchpadActions(axis) { HapticMode = HapticMode.Down, HapticStrength = HapticStrength.Low }]
                        : [new AxisActions { Axis = axis }];
                    break;
                case AxisLayoutFlags.Gyroscope:
                    break;
                case AxisLayoutFlags.L2:
                case AxisLayoutFlags.R2:
                    AxisLayout[axis] = new List<IActions> { new TriggerActions { Axis = axis } };
                    break;
            }
        }
    }

    public object Clone()
    {
        lock (_sync)
            return CloningHelper.DeepClone(this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            ButtonLayout.Clear();
            AxisLayout.Clear();
            GyroLayout.Clear();
        }

        GC.SuppressFinalize(this);
    }

    public void UpdateLayout()
    {
        Updated?.Invoke(this);
    }

    public void UpdateLayout(ButtonFlags button, List<IActions> actions)
    {
        lock (_sync)
            ButtonLayout[button] = actions.OrderByDescending(a => (int)a.pressType).ToList();

        Updated?.Invoke(this);
    }

    public void UpdateLayout(AxisLayoutFlags axis, List<IActions> actions)
    {
        lock (_sync)
        {
            switch (axis)
            {
                default:
                    AxisLayout[axis] = actions;
                    break;
            }
        }

        Updated?.Invoke(this);
    }

    public void UpdateLayout(AxisLayoutFlags axis, IActions action)
    {
        lock (_sync)
        {
            if (axis == AxisLayoutFlags.Gyroscope)
                GyroLayout[axis] = action;
        }

        Updated?.Invoke(this);
    }

    public void RemoveLayout(ButtonFlags button)
    {
        lock (_sync)
            ButtonLayout.Remove(button);

        Updated?.Invoke(this);
    }

    public void RemoveLayout(AxisLayoutFlags axis)
    {
        lock (_sync)
        {
            if (axis == AxisLayoutFlags.Gyroscope)
                GyroLayout.Remove(axis);
            else
                AxisLayout.Remove(axis);
        }

        Updated?.Invoke(this);
    }

    #region events
    public event UpdatedEventHandler? Updated;
    public delegate void UpdatedEventHandler(Layout layout);
    #endregion
}
