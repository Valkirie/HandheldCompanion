using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public abstract class ICommands : ICloneable, IDisposable
    {
        [Serializable]
        public enum CommandType
        {
            None = 0,
            Function = 1,
            Keyboard = 2,
            Executable = 3,
        }

        public delegate void ExecutedEventHandler(ICommands command);
        public event ExecutedEventHandler Executed;

        public delegate void UpdatedEventHandler(ICommands command);
        public event UpdatedEventHandler Updated;

        protected object Value;
        protected object prevValue;

        [JsonIgnore] public bool OnKeyDown = false;
        [JsonIgnore] public bool OnKeyUp = false;
        [JsonIgnore] public Hotkey Hotkey;

        [JsonIgnore] public string Name = "Empty hotkey";
        [JsonIgnore] public string Description = "Please pick a command type";
        [JsonIgnore] public string Glyph = "\ue895";

        [JsonIgnore] private string _LiveGlyph = string.Empty;
        [JsonIgnore]
        public string LiveGlyph
        {
            get
            {
                if (string.IsNullOrEmpty(_LiveGlyph))
                    return Glyph;

                return _LiveGlyph;
            }
            set
            {
                if (value != _LiveGlyph)
                    _LiveGlyph = value;
            }
        }

        [JsonIgnore] public string FontFamily = "Segoe Fluent Icons";

        public CommandType commandType;

        public ICommands() { }

        public virtual void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            Executed?.Invoke(this);
        }

        public virtual void Update()
        {
            Updated?.Invoke(this);
        }

        [JsonIgnore] public virtual bool IsToggled => false;
        [JsonIgnore] public bool IsEnabled = true;

        public virtual object Clone()
        {
            return this;
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
