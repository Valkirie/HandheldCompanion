using ControllerCommon.Inputs;
using SharpDX.XInput;
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

        // todo: move me and make me configurable !
        [JsonIgnore]
        public static readonly Dictionary<AxisLayoutFlags, short> AxisDeadzones = new()
        {
            { AxisLayoutFlags.RightThumb, Gamepad.RightThumbDeadZone },
            { AxisLayoutFlags.LeftThumb, Gamepad.LeftThumbDeadZone },
            { AxisLayoutFlags.L2, Gamepad.TriggerThreshold },
            { AxisLayoutFlags.R2, Gamepad.TriggerThreshold },
        };

        [JsonIgnore]
        public static readonly Dictionary<AxisLayoutFlags, ButtonFlags> AxisTouchButtons = new()
        {
            { AxisLayoutFlags.RightThumb, ButtonFlags.RightThumbTouch },
            { AxisLayoutFlags.LeftThumb, ButtonFlags.LeftThumbTouch },
            { AxisLayoutFlags.RightPad, ButtonFlags.RightPadTouch },
            { AxisLayoutFlags.LeftPad, ButtonFlags.LeftPadTouch },
        };

        public int Timestamp;

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
