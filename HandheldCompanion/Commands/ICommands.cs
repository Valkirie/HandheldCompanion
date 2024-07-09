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

        protected object Value;
        protected object prevValue;

        [JsonIgnore] public bool OnKeyDown = false;
        [JsonIgnore] public bool OnKeyUp = false;
        [JsonIgnore] public Hotkey Hotkey;

        [JsonIgnore] public string Name = "Missing name";
        [JsonIgnore] public string Description = "Missing description";
        [JsonIgnore] public string Glyph = "\ue895";

        public CommandType commandType;

        public ICommands() { }

        public virtual void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            Executed?.Invoke(this);
        }

        [JsonIgnore] public virtual bool IsToggled => false;

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
