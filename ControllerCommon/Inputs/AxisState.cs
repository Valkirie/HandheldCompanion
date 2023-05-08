using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public class AxisState : ICloneable
    {
        public short[] State = new short[(int)AxisFlags.MaxValue];

        public short this[AxisFlags axis]
        {
            get
            {
                return State[(int)axis];
            }

            set
            {
                State[(int)axis] = value;
            }
        }

        [JsonIgnore]
        public IEnumerable<AxisFlags> Axis => GetAxis();

        public AxisState(short[] axisState)
        {
            for (int i = 0; i < State.Length; i++)
                State[i] = axisState[i];
        }

        public AxisState()
        {
        }

        private List<AxisFlags> GetAxis()
        {
            List<AxisFlags> buttons = new();
            for (int i = 0; i < State.Length; i++)
                if (State[i] != 0)
                    buttons.Add((AxisFlags)i);

            return buttons;
        }

        public bool IsEmpty()
        {
            return Axis.Count() == 0;
        }

        public void Clear()
        {
            for (int i = 0; i < State.Length; i++)
                State[i] = 0;
        }

        public bool Contains(AxisState axisState)
        {
            for (int i = 0; i < axisState.State.Length; i++)
                if (State[i] != axisState.State[i])
                    return false;

            return true;
        }

        public bool ContainsTrue(AxisState axisState)
        {
            if (this.IsEmpty() || axisState.IsEmpty())
                return false;

            for (int i = 0; i < axisState.State.Length; i++)
                if (axisState.State[i] != 0)
                    if (State[i] != axisState.State[i])
                        return false;

            return true;
        }

        public void AddRange(AxisState axisState)
        {
            for (int i = 0; i < axisState.State.Length; i++)
                State[i] = axisState.State[i];
        }

        public override bool Equals(object obj)
        {
            AxisState axisState = obj as AxisState;
            if (axisState is null)
                return false;

            for (int i = 0; i < axisState.State.Length; i++)
            {
                if (State[i] == axisState.State[i])
                    continue;

                return false;
            }

            return true;
        }

        public object Clone()
        {
            return new AxisState(State);
        }
    }
}
