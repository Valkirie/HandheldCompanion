using ControllerCommon.Utils;
using SharpDX.XInput;
using System;

namespace HandheldCompanion.Managers.Classes
{
    [Flags]
    public enum InputsChordType : ushort
    {
        None = 0,
        Gamepad = 1,
        Keyboard = 2,
    }

    public class InputsChord
    {
        public InputsChordType type { get; set; }
        public GamepadButtonFlags buttons { get; set; }
        public string name { get; set; }

        public InputsChord(InputsChordType type, string value)
        {
            this.type = type;

            switch (type)
            {
                default:
                case InputsChordType.Gamepad:
                    buttons = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), value, true);
                    break;

                case InputsChordType.Keyboard:
                    name = value;
                    break;
            }
        }

        public InputsChord(InputsChordType type, string value, string name) : this(type, value)
        {
            this.name = name;
        }

        public InputsChord()
        {
        }
    }
}
