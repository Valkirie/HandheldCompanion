using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HandheldCompanion.Controllers
{
    [Serializable]
    public class ControllerState : ICloneable
    {
        public ButtonState ButtonState = new();
        public AxisState AxisState = new();
        public GyroState GyroState = new();

        [JsonIgnore]
        public static readonly SortedDictionary<AxisLayoutFlags, ButtonFlags> AxisTouchButtons = new()
        {
            { AxisLayoutFlags.RightStick, ButtonFlags.RightStickTouch },
            { AxisLayoutFlags.LeftStick, ButtonFlags.LeftStickTouch },
            { AxisLayoutFlags.RightPad, ButtonFlags.RightPadTouch },
            { AxisLayoutFlags.LeftPad, ButtonFlags.LeftPadTouch },
        };

        public ControllerState()
        { }

        public object Clone()
        {
            return new ControllerState()
            {
                ButtonState = this.ButtonState.Clone() as ButtonState,
                AxisState = this.AxisState.Clone() as AxisState,
                GyroState = this.GyroState.Clone() as GyroState,
            };
        }
    }
}