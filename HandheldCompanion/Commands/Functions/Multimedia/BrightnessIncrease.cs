using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.Multimedia
{
    [Serializable]
    public class BrightnessIncrease : FunctionCommands
    {
        public BrightnessIncrease()
        {
            Name = Properties.Resources.Hotkey_increaseBrightness;
            Description = Properties.Resources.Hotkey_increaseBrightnessDesc;
            Glyph = "\uE706";
            OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            MultimediaManager.IncreaseBrightness();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            BrightnessIncrease commands = new()
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
