using Gma.System.MouseKeyHook;
using SharpDX.XInput;
using System;
using System.Collections.Generic;

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
        public GamepadButtonFlags buttons { get; set; } = GamepadButtonFlags.None;
        public string key { get; set; } = string.Empty;
        public List<KeyEventArgsExt> combo { get; set; } = new();

        public InputsChordType type { get; set; } = InputsChordType.Click;

        public InputsChord(GamepadButtonFlags buttons, string key, List<KeyEventArgsExt> combo, InputsChordType type)
        {
            this.buttons = buttons;
            this.key = key;
            this.combo = combo;

            this.type = type;
        }

        public InputsChord()
        {
        }
    }
}
