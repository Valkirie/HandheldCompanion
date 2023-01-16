using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public class AxisState : ICloneable
    {
        [JsonInclude]
        public Dictionary<AxisFlags, short> State = new();

        public short this[AxisFlags axis]
        {
            get
            {
                if (!State.ContainsKey(axis))
                {
                    return 0;
                }

                return State[axis];
            }

            set
            {
                State[axis] = value;
            }
        }

        [JsonIgnore]
        public IEnumerable<AxisFlags> Axis => State.Where(a => a.Value != 0).Select(a => a.Key).ToList();

        public AxisState(Dictionary<AxisFlags, short> axisState)
        {
            foreach (var state in axisState)
                this[state.Key] = state.Value;
        }

        public AxisState()
        {
        }

        public bool IsEmpty()
        {
            return Axis.Count() == 0;
        }

        public void Clear()
        {
            State.Clear();
        }

        public bool Contains(AxisState axisState)
        {
            foreach (var state in axisState.State)
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public void AddRange(AxisState axisState)
        {
            foreach (var state in axisState.State)
                this[state.Key] = state.Value;
        }

        public override bool Equals(object obj)
        {
            AxisState axisState = obj as AxisState;
            if (axisState != null)
                return EqualsWithValues(State, axisState.State);

            return false;
        }

        public static bool EqualsWithValues<TKey, TValue>(Dictionary<TKey, TValue> obj1, Dictionary<TKey, TValue> obj2)
        {
            bool result = false;
            if (obj1.Count == obj2.Count)
            {
                result = true;
                {
                    foreach (KeyValuePair<TKey, TValue> item in obj1)
                    {
                        if (obj2.TryGetValue(item.Key, out var value))
                        {
                            if (!value.Equals(item.Value))
                            {
                                return false;
                            }

                            continue;
                        }

                        return false;
                    }

                    return result;
                }
            }

            return result;
        }

        public object Clone()
        {
            return new AxisState(State);
        }
    }
}
