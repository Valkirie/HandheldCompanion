using ControllerCommon.Inputs;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Documents;

namespace ControllerCommon.Controllers
{
    [Serializable]
    public class ControllerState : ICloneable
    {
        public ButtonState ButtonState = new();
        public AxisState AxisState = new();

        [JsonIgnore]
        public static Dictionary<AxisFlags, short> AxisDeadzones = new()
        {
            { AxisFlags.RightThumbX, Gamepad.RightThumbDeadZone },
            { AxisFlags.RightThumbY, Gamepad.RightThumbDeadZone },
            { AxisFlags.LeftThumbY, Gamepad.LeftThumbDeadZone },
            { AxisFlags.LeftThumbX, Gamepad.LeftThumbDeadZone },
            { AxisFlags.L2, Gamepad.TriggerThreshold },
            { AxisFlags.R2, Gamepad.TriggerThreshold },
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
