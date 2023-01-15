using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Inputs
{
    public class ButtonState : ICloneable
    {
        private Dictionary<ButtonFlags, bool> _buttonState = new();

        public bool this[ButtonFlags button]
        {
            get
            {
                if (!_buttonState.ContainsKey(button))
                {
                    return false;
                }

                return _buttonState[button];
            }

            set
            {
                switch(value)
                {
                    case true:
                        _buttonState[button] = true;
                        break;
                    case false:
                        _buttonState.Remove(button);
                        break;
                }
            }
        }

        public IEnumerable<ButtonFlags> Buttons => _buttonState.Keys;

        public ButtonState(Dictionary<ButtonFlags, bool> buttonState)
        {
            foreach (var state in buttonState)
                _buttonState.Add(state.Key, state.Value);
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
            _buttonState.Clear();
        }

        public bool Contains(ButtonState State)
        {
            foreach (var state in State._buttonState)
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            ButtonState buttonState = obj as ButtonState;
            if (buttonState != null)
            {
                return EqualsWithValues(_buttonState, buttonState._buttonState);
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

        public object Clone()
        {
            return new ButtonState(_buttonState);
        }
    }
}
