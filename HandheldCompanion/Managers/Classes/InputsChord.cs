using SharpDX.XInput;
using System;

namespace HandheldCompanion.Managers.Classes
{
    [Flags]
    public enum InputsChordType : ushort
    {
        None = 0,
        Click = 1,
        Hold = 2,
    }

    public class InputsChord
    {
        public GamepadButtonFlags buttons { get; set; }
        public string key { get; set; }
        public InputsChordType type { get; set; }

        public InputsChord(string buttons, string key, InputsChordType type)
        {
            this.key = key;
            this.type = type;

            this.buttons = (GamepadButtonFlags)Enum.Parse(typeof(GamepadButtonFlags), buttons, true);
        }

        public InputsChord(GamepadButtonFlags buttons, string key, InputsChordType type)
        {
            this.key = key;
            this.type = type;

            this.buttons = buttons;
        }

        public InputsChord()
        {
        }
    }
}
