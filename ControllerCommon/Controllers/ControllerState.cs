using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ControllerCommon.Inputs;
using MemoryPack;

namespace ControllerCommon.Controllers;

[Serializable]
[MemoryPackable]
public partial class ControllerState : ICloneable
{
    [JsonIgnore] [MemoryPackIgnore] public static readonly SortedDictionary<AxisLayoutFlags, ButtonFlags> AxisTouchButtons = new()
    {
        { AxisLayoutFlags.RightStick, ButtonFlags.RightStickTouch },
        { AxisLayoutFlags.LeftStick, ButtonFlags.LeftStickTouch },
        { AxisLayoutFlags.RightPad, ButtonFlags.RightPadTouch },
        { AxisLayoutFlags.LeftPad, ButtonFlags.LeftPadTouch }
    };

    public AxisState AxisState = new();
    public ButtonState ButtonState = new();

    public ControllerState()
    {
    }

    [MemoryPackConstructor]
    public ControllerState(ButtonState buttonState, AxisState axisState, int timestamp)
    {
        ButtonState = buttonState;
        AxisState = axisState;

        Timestamp = timestamp;
    }

    public int Timestamp { get; set; }
    public bool MotionTriggered { get; set; }

    public object Clone()
    {
        return new ControllerState
        {
            ButtonState = ButtonState.Clone() as ButtonState,
            AxisState = AxisState.Clone() as AxisState,
            Timestamp = Timestamp
        };
    }
}