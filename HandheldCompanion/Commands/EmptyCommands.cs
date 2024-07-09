using System;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class EmptyCommands : ICommands
    {
        public EmptyCommands()
        {
            base.commandType = CommandType.None;
        }
    }
}
