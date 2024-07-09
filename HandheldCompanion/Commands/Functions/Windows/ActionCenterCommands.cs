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

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo("ms-actioncenter://") { UseShellExecute = true });
            });

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            ActionCenterCommands commands = new();
            commands.commandType = commandType;
            commands.Name = Name;
            commands.Description = Description;
            commands.Glyph = Glyph;
            commands.OnKeyUp = OnKeyUp;
            commands.OnKeyDown = OnKeyDown;

            return commands;
        }
    }
}
