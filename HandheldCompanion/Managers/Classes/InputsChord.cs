using SharpDX.XInput;
using System;

namespace HandheldCompanion.Managers.Classes
{
    [Flags]
    public enum InputsChordFamily : ushort
    {
        None = 0,
        Gamepad = 1,
        Keyboard = 2,
    }

    [Flags]
    public enum InputsChordType : ushort
    {
        Click = 0,
        Hold = 1,
    }

    public class InputsChord
    {
        public InputsChordFamily family { get; set; }
        public GamepadButtonFlags buttons { get; set; }
        public string name { get; set; }
        public InputsChordType type { get; set; }

        public InputsChord(InputsChordFamily family, string value, string name, InputsChordType type)
        {
            this.name = name;
            this.family = family;
            this.type = type;

            switch (family)
            {
                case InputsChordFamily.Gamepad:
                    this.buttons = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), value, true);
                    break;

                case InputsChordFamily.Keyboard:
                    break;
            }
        }

        public InputsChord()
        {
        }
    }
}
