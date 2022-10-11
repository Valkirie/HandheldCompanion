﻿using SharpDX.XInput;
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
        public GamepadButtonFlags buttons { get; set; } = GamepadButtonFlags.None;
        public string key { get; set; } = string.Empty;
        public InputsChordType type { get; set; } = InputsChordType.Click;

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
