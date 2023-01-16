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
    public class ButtonState : ICloneable
    {
        [JsonInclude]
        public Dictionary<ButtonFlags, bool> State = new();

        public bool this[ButtonFlags button]
        {
            get
            {
                if (!State.ContainsKey(button))
                {
                    return false;
                }

                return State[button];
            }

            set
            {
                switch(value)
                {
                    case true:
                        State[button] = true;
                        break;
                    case false:
                        State.Remove(button);
                        break;
                }
            }
        }

        [JsonIgnore]
        public IEnumerable<ButtonFlags> Buttons => State.Keys;

        public ButtonState(Dictionary<ButtonFlags, bool> buttonState)
        {
            foreach (var state in buttonState)
                this[state.Key] = state.Value;
        }

        public ButtonState()
        {
        }

        public bool IsEmpty()
        {
            return State.Count() == 0;
        }

        public void Clear()
        {
            State.Clear();
        }

        public bool Contains(ButtonState State)
        {
            foreach (var state in State.State)
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public void AddRange(IEnumerable<ButtonFlags> buttons)
        {
            foreach (ButtonFlags button in buttons)
                this[button] = true;
        }

        public override bool Equals(object obj)
        {
            ButtonState buttonState = obj as ButtonState;
            if (buttonState != null)
                return EqualsWithValues(State, buttonState.State);

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
            return new ButtonState(State);
        }
    }
}
