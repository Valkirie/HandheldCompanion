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

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            MultimediaManager.IncreaseVolume();

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            VolumeIncrease commands = new()
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
