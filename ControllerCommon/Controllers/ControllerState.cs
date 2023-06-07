using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ControllerCommon.Inputs;

namespace ControllerCommon.Controllers;

[Serializable]
public class ControllerState : ICloneable
{
    [JsonIgnore] public static readonly SortedDictionary<AxisLayoutFlags, ButtonFlags> AxisTouchButtons = new()
    {
        { AxisLayoutFlags.RightThumb, ButtonFlags.RightThumbTouch },
        { AxisLayoutFlags.LeftThumb, ButtonFlags.LeftThumbTouch },
        { AxisLayoutFlags.RightPad, ButtonFlags.RightPadTouch },
        { AxisLayoutFlags.LeftPad, ButtonFlags.LeftPadTouch }
    };

    public AxisState AxisState = new();
    public ButtonState ButtonState = new();

    public ControllerState()
    {
    }

    public ControllerState(ControllerState Inputs)
    {
        ButtonState = Inputs.ButtonState;
        AxisState = Inputs.AxisState;

        Timestamp = Inputs.Timestamp;
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