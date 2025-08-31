using HandheldCompanion.Commands;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WindowsInput.Events;

namespace HandheldCompanion
{
    [Serializable]
    public class Hotkey : ICloneable, IDisposable
    {
        public ButtonFlags ButtonFlags { get; set; }

        private ICommands _command = new EmptyCommands();
        public ICommands command
        {
            get { return _command; }
            set
            {
                _command = value;
                this.Name = command.Name;
            }
        }

        public InputsChord inputsChord { get; set; } = new() { chordType = InputsChordType.Click };

        public bool IsPinned { get; set; } = false;
        public int PinIndex { get; set; } = -1;
        public bool IsInternal { get; set; } = false;

        public string Name { get; set; } = string.Empty;
        public Version Version { get; set; } = new();

        [JsonIgnore]
        public string FileName { get; set; } = string.Empty;

        public InputsChordType InputsChordType
        {
            get { return inputsChord.chordType; }
            set { inputsChord.chordType = value; }
        }

        private bool _disposed = false; // Prevent multiple disposals

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
            this.ButtonFlags = ManagerFactory.hotkeysManager.GetAvailableButtonFlag();
            if (this.ButtonFlags == ButtonFlags.None)
                throw new InvalidOperationException("No available ButtonFlags.");
        }

        public Hotkey(ButtonFlags buttonFlags)
        {
            this.ButtonFlags = buttonFlags;
        }

        ~Hotkey()
        {
            Dispose(false);
        }

        public override string ToString()
        {
            return Name;
        }

        public string GetFileName()
        {
            return $"{ButtonFlags}.json";
        }

        public void SetChordType(InputsChordType chordType)
        {
            this.inputsChord.chordType = chordType;
        }

        public void SetChordButton(ButtonFlags buttonFlags)
        {
            this.inputsChord.ButtonState[buttonFlags] = true;
        }

        public object Clone()
        {
            Hotkey hotkey = new(ButtonFlags)
            {
                command = this.command?.Clone() as ICommands,
                inputsChord = this.inputsChord?.Clone() as InputsChord,
                IsPinned = this.IsPinned,
                IsInternal = this.IsInternal
            };

            return hotkey;
        }

        public void Execute(bool onKeyDown, bool onKeyUp, bool IsBackground)
        {
            bool Rumble = ManagerFactory.settingsManager.GetBoolean("HotkeyRumbleOnExecution");
            if (Rumble && !IsBackground && !IsInternal)
                ControllerManager.GetTarget()?.Rumble();

            command?.Execute(command.OnKeyDown && onKeyDown, command.OnKeyUp && onKeyUp, IsBackground);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Free managed resources
                command?.Dispose();
                command = null;

                inputsChord.Dispose();
                inputsChord = null;
            }

            _disposed = true;
        }
    }
}