using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public class ButtonState : ICloneable
    {
        public bool[] State = new bool[(int)ButtonFlags.MaxValue];

        public bool this[ButtonFlags button]
        {
            get
            {
                return State[(int)button];
            }

            set
            {
                State[(int)button] = value;
            }
        }

        [JsonIgnore]
        public IEnumerable<ButtonFlags> Buttons => GetButtons();

        public ButtonState(bool[] buttonState)
        {
            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                State[i] = buttonState[i];
        }

        public ButtonState()
        {
        }

        private List<ButtonFlags> GetButtons()
        {
            List<ButtonFlags > buttons = new();
            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                if (State[i])
                    buttons.Add((ButtonFlags)i);

            return buttons;
        }

        public bool IsEmpty()
        {
            return Buttons.Count() == 0;
        }

        public void Clear()
        {
            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                State[i] = false;
        }

        public bool Contains(ButtonState buttonState)
        {
            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                if (State[i] != buttonState.State[i])
                    return false;

            return true;
        }

        public bool ContainsTrue(ButtonState buttonState)
        {
            if (this.IsEmpty() || buttonState.IsEmpty())
                return false;

            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                if (buttonState.State[i])
                    if (State[i] != buttonState.State[i])
                        return false;

            return true;
        }

        public void AddRange(ButtonState buttonState)
        {
            // only add pressed button
            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
                if (buttonState.State[i])
                    State[i] = buttonState.State[i];
        }

        public override bool Equals(object obj)
        {
            ButtonState buttonState = obj as ButtonState;
            if (buttonState is null)
                return false;

            for (int i = 0; i < (int)ButtonFlags.MaxValue; i++)
            {
                if (State[i] == buttonState.State[i])
                    continue;

                return false;
            }

            return true;
        }

        public object Clone()
        {
            return new ButtonState(State);
        }
    }
}
