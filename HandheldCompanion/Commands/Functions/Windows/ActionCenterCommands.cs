using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class ActionCenterCommands : FunctionCommands
    {
        public ActionCenterCommands()
        {
            Name = Properties.Resources.Hotkey_ActionCenter;
            Description = Properties.Resources.Hotkey_ActionCenterDesc;
            Glyph = "\ue91c";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo("ms-actioncenter://") { UseShellExecute = true });
            });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            ActionCenterCommands commands = new()
            {
                commandType = commandType,
                Name = Name,
                Description = Description,
                Glyph = Glyph,
                OnKeyUp = OnKeyUp,
                OnKeyDown = OnKeyDown
            };

            return commands;
        }
    }
}
