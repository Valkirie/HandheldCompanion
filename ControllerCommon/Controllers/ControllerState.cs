using ControllerCommon.Inputs;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControllerCommon.Controllers
{
    [Serializable]
    public class ControllerState : ICloneable
    {
        public ButtonState ButtonState = new();
        public AxisState AxisState = new();

        public int Timestamp { get; set; }
        public bool MotionTriggered { get; set; }

        [JsonIgnore]
        public static readonly SortedDictionary<AxisLayoutFlags, ButtonFlags> AxisTouchButtons = new()
        {
            { AxisLayoutFlags.RightThumb, ButtonFlags.RightThumbTouch },
            { AxisLayoutFlags.LeftThumb, ButtonFlags.LeftThumbTouch },
            { AxisLayoutFlags.RightPad, ButtonFlags.RightPadTouch },
            { AxisLayoutFlags.LeftPad, ButtonFlags.LeftPadTouch },
        };

        public ControllerState()
        { }

        public ControllerState(ControllerState Inputs)
        {
            ButtonState = Inputs.ButtonState;
            AxisState = Inputs.AxisState;

            Timestamp = Inputs.Timestamp;
        }

        public object Clone()
        {
            return new ControllerState()
            {
                ButtonState = this.ButtonState.Clone() as ButtonState,
                AxisState = this.AxisState.Clone() as AxisState,
                Timestamp = this.Timestamp
            };
        }
    }
}
