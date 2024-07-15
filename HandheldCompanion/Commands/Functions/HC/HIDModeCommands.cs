using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class HIDModeCommands : FunctionCommands
    {
        public HIDModeCommands()
        {
            base.Name = Properties.Resources.Hotkey_ChangeHIDMode;
            base.Description = Properties.Resources.Hotkey_ChangeHIDModeDesc;
            base.Glyph = "\ue7fc";
            base.OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            HIDmode currentHIDmode = (HIDmode)SettingsManager.GetInt("HIDmode", true);
            switch (currentHIDmode)
            {
                case HIDmode.Xbox360Controller:
                    SettingsManager.SetProperty("HIDmode", (int)HIDmode.DualShock4Controller);
                    break;
                case HIDmode.DualShock4Controller:
                    SettingsManager.SetProperty("HIDmode", (int)HIDmode.Xbox360Controller);
                    break;
                default:
                    break;
            }

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            HIDModeCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown
            };

            return commands;
        }
    }
}
