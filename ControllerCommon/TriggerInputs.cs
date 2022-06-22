using SharpDX.XInput;
using System;

namespace ControllerCommon
{
    [Flags]
    public enum TriggerInputsType
    {
        Gamepad,
        Keyboard
    }

    public class TriggerInputs
    {
        public TriggerInputsType type;
        public GamepadButtonFlags buttons;
        public string raw;
        public string name;

        public TriggerInputs(TriggerInputsType type, string value)
        {
            this.type = type;
            this.raw = value;

            switch (type)
            {
                default:
                case TriggerInputsType.Gamepad:
                    {
                        this.buttons = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), value, true);
                    }
                    break;

                case TriggerInputsType.Keyboard:
                    {
                        this.name = value;
                    }
                    break;
            }
        }

        public TriggerInputs(TriggerInputsType type, string value, string name) : this(type, value)
        {
            this.name = name;
        }

        public string GetValue()
        {
            switch (type)
            {
                default:
                case TriggerInputsType.Gamepad:
                    return this.buttons.ToString();

                case TriggerInputsType.Keyboard:
                    return this.name;
            }
        }
    }
}
