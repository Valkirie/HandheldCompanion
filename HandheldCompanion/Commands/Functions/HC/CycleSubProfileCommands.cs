using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class CycleSubProfileCommands : FunctionCommands
    {
        public int CycleIndex = 0;

        public CycleSubProfileCommands()
        {
            base.Name = Properties.Resources.Hotkey_CycleSubProfile;
            base.Description = Properties.Resources.Hotkey_CycleSubProfileDesc;
            base.Glyph = "\uE81E";
            base.OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            switch (CycleIndex)
            {
                case 0:
                    ProfileManager.CycleSubProfiles(true);
                    break;
                case 1:
                    ProfileManager.CycleSubProfiles(false);
                    break;
            }

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            CycleSubProfileCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown,
                CycleIndex = this.CycleIndex,
            };

            return commands;
        }
    }
}
