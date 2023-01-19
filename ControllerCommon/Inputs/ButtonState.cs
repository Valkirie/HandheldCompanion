using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public class ButtonState : ICloneable
    {
        public Dictionary<ButtonFlags, bool> State = new();
        [JsonIgnore]
        public Dictionary<ButtonFlags, bool> Emulated = new();

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
                State[button] = value;
                Emulated[button] = false;
            }
        }

        [JsonIgnore]
        public IEnumerable<ButtonFlags> Buttons => State.Where(a => a.Value is true).Select(a => a.Key).ToList();

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
            return Buttons.Count() == 0;
        }

        public void Clear()
        {
            State.Clear();
        }

        public bool Contains(ButtonState buttonState)
        {
            foreach (var state in buttonState.State)
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public void AddRange(ButtonState buttonState)
        {
            foreach (var state in buttonState.State)
                this[state.Key] = state.Value;
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
