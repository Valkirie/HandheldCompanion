using System;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class EmptyCommands : ICommands
    {
        public EmptyCommands()
        {
            base.commandType = CommandType.None;
            base.OnKeyUp = true;
            base.OnKeyDown = true;
        }

        public virtual void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            base.Execute(IsKeyDown, IsKeyUp, IsBackground);
        }
    }
}
