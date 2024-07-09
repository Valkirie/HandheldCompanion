using HandheldCompanion.Actions;
using HandheldCompanion.Commands.Functions;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
