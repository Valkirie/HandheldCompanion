using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.Multimedia
{
    [Serializable]
    public class VolumeIncrease : FunctionCommands
    {
        public VolumeIncrease()
        {
            Name = Properties.Resources.Hotkey_increaseVolume;
            Description = Properties.Resources.Hotkey_increaseVolumeDesc;
            Glyph = "\uE995";
            OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            MultimediaManager.IncreaseVolume();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            VolumeIncrease commands = new();
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
