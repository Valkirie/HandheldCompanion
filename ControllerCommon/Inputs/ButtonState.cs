using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public class ButtonState : ICloneable
    {
        public SortedDictionary<ButtonFlags, bool> State = new();

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
            }
        }

        [JsonIgnore]
        public IEnumerable<ButtonFlags> Buttons => State.Where(a => a.Value is true).Select(a => a.Key).ToList();

        public ButtonState(SortedDictionary<ButtonFlags, bool> buttonState)
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

        public bool ContainsTrue(ButtonState buttonState)
        {
            if (this.IsEmpty() || buttonState.IsEmpty())
                return false;

            foreach (var state in buttonState.State.Where(a => a.Value is true))
                if (this[state.Key] != state.Value)
                    return false;

            return true;
        }

        public void AddRange(ButtonState buttonState)
        {
            // only add pressed button
            foreach (var state in buttonState.State.Where(a => a.Value))
                this[state.Key] = state.Value;
        }

        public override bool Equals(object obj)
        {
            ButtonState buttonState = obj as ButtonState;
            if (buttonState != null)
                return buttonState.Buttons.SequenceEqual(Buttons);

            return false;
        }

        public object Clone()
        {
            return new ButtonState(State);
        }
    }
}
