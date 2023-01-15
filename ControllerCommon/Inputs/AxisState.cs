using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControllerCommon.Inputs
{
    public class AxisState
    {
        private Dictionary<AxisFlags, float> _axisState;

        public float this[AxisFlags axis]
        {
            get
            {
                if (!_axisState.ContainsKey(axis))
                {
                    return 0.0f;
                }

                return _axisState[axis];
            }

            set
            {
                _axisState[axis] = value;
            }
        }

        public IEnumerable<AxisFlags> Axis => _axisState.Keys;

        public AxisState(Dictionary<AxisFlags, float> axisState)
        {
            _axisState = axisState;
        }

        public AxisState()
        {
            _axisState = new();
        }

        public bool IsEmpty()
        {
            return Axis.Count() == 0;
        }

        public void Clear()
        {
            _axisState.Clear();
        }

        public void Merge(AxisState State)
        {
            foreach (var state in State._axisState)
                this[state.Key] = state.Value;
        }

        public bool Contains(AxisState State)
        {
            foreach (var state in State._axisState)
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            AxisState axisState = obj as AxisState;
            if (axisState != null)
            {
                return EqualsWithValues(_axisState, axisState._axisState);
            }

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
    }
}
