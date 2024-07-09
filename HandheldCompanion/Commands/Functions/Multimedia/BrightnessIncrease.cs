using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
            BrightnessIncrease commands = new();
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
