using HandheldCompanion.Commands;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace HandheldCompanion
{
    [Serializable]
    public class Hotkey : ICloneable
    {
        public ButtonFlags ButtonFlags { get; set; }

        public ICommands command { get; set; } = new EmptyCommands();
        public InputsChord inputsChord { get; set; } = new();

        public bool IsPinned { get; set; } = false;
        public bool IsInternal { get; set; } = false;

        public string Name { get; set; } = string.Empty;
        public Version Version { get; set; } = new();

        [JsonIgnore]
        public KeyboardChord keyChord
        {
            get
            {
                List<KeyCode> chordDown = inputsChord.KeyState.Where(key => key.IsKeyDown).Select(key => (KeyCode)key.KeyValue).ToList();
                List<KeyCode> chordUp = inputsChord.KeyState.Where(key => key.IsKeyUp).Select(key => (KeyCode)key.KeyValue).ToList();
                return new($"Hotkey_{(int)ButtonFlags}", chordDown, chordUp, false, ButtonFlags);
            }
        }

        public Hotkey()
        {
            this.ButtonFlags = HotkeysManager.GetAvailableButtonFlag();
            if (this.ButtonFlags == ButtonFlags.None)
                throw new InvalidOperationException("No available ButtonFlags.");
        }

        public Hotkey(ButtonFlags buttonFlags)
        {
            this.ButtonFlags = buttonFlags;
        }

        public object Clone()
        {
            Hotkey hotkey = new(ButtonFlags)
            {
                command = this.command.Clone() as ICommands,
                inputsChord = this.inputsChord.Clone() as InputsChord,
                IsPinned = this.IsPinned,
                IsInternal = this.IsInternal
            };

            return hotkey;
        }
    }
}
