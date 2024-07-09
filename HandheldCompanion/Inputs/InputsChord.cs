using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Inputs
{
    [Flags]
    public enum InputsChordType : ushort
    {
        None = 0,
        Click = 1,
        Long = 2
    }

    [Flags]
    public enum InputsChordTarget : ushort
    {
        Input = 0,
        Output = 1,
    }

    [Serializable]
    public class InputsChord : ICloneable
    {
        public InputsChord(ButtonState buttonState, List<InputsKey> keyState, InputsChordType InputsType)
        {
            this.ButtonState = buttonState.Clone() as ButtonState;
            this.KeyState.AddRange(keyState);
            this.chordType = InputsType;
        }

        public InputsChord() { }

        public ButtonState ButtonState { get; set; } = new();
        public List<InputsKey> KeyState { get; set; } = new();

        private InputsChordType _chordType { get; set; } = InputsChordType.Click;
        public InputsChordType chordType
        {
            get
            {
                if (ButtonState.Buttons.Count() == 0 && KeyState.Count == 0)
                    return InputsChordType.None;
                else
                    return _chordType;
            }
            set
            {
                _chordType = value;
            }
        }

        public InputsChordTarget chordTarget = InputsChordTarget.Input;

        public void AddKey(KeyEventArgsExt args)
        {
            InputsKey key = new InputsKey
            {
                KeyValue = args.KeyValue,
                ScanCode = args.ScanCode,
                Timestamp = args.Timestamp,
                IsKeyDown = args.IsKeyDown,
                IsKeyUp = args.IsKeyUp,
                IsExtendedKey = args.IsExtendedKey
            };

            KeyState.Add(key);
        }

        public bool HasKey(KeyEventArgsExt args)
        {
            return KeyState.Any(key => key.KeyValue == args.KeyValue && key.IsKeyUp == args.IsKeyUp && key.IsKeyDown == args.IsKeyDown);
        }

        public object Clone()
        {
            InputsChord inputsChord = new();
            inputsChord.KeyState.AddRange(this.KeyState);
            inputsChord.ButtonState = this.ButtonState.Clone() as ButtonState;
            inputsChord.chordType = this.chordType;

            return inputsChord;
        }
    }
}
