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
            PowerShell = 4,
        }

        public delegate void ExecutedEventHandler(ICommands command);
        public event ExecutedEventHandler Executed;

        public delegate void UpdatedEventHandler(ICommands command);
        public event UpdatedEventHandler Updated;

        protected object Value;
        protected object prevValue;

        [JsonIgnore] public bool OnKeyDown = false;
        [JsonIgnore] public bool OnKeyUp = false;

        [JsonIgnore] public string Name = Properties.Resources.Hotkey_DefaultName;
        [JsonIgnore] public string Description = Properties.Resources.Hotkey_DefaultDesc;
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

        [JsonIgnore] private string _LiveName = string.Empty;
        [JsonIgnore]
        public string LiveName
        {
            get
            {
                if (string.IsNullOrEmpty(_LiveName))
                    return Glyph;

                return _LiveName;
            }
            set
            {
                if (value != _LiveName)
                    _LiveName = value;
            }
        }

        [JsonIgnore] public string FontFamily = "Segoe Fluent Icons";
        [JsonIgnore] public int FontSize = 16;

        public CommandType commandType;
        public Type deviceType = null;

        private bool _disposed = false; // Prevent multiple disposals

        public ICommands() { }

        ~ICommands()
        {
            Dispose(false);
        }

        public virtual void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            Executed?.Invoke(this);
        }

        public virtual void Update()
        {
            Updated?.Invoke(this);
        }

        [JsonIgnore] public virtual bool IsToggled => false;

        [JsonIgnore] public bool IsEnabled = true;
        [JsonIgnore] public bool CanCustom = true;
        [JsonIgnore] public bool CanUnpin = true;

        public virtual object Clone()
        {
            return MemberwiseClone();
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Release managed resources
                Executed = null;
                Updated = null;
            }

            _disposed = true;
        }
    }
}
