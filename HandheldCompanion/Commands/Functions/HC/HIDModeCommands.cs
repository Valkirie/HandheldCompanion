using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class HIDModeCommands : FunctionCommands
    {
        private const string SettingsName = "HIDmode";

        public HIDModeCommands()
        {
            base.Name = Properties.Resources.Hotkey_ChangeHIDMode;
            base.Description = Properties.Resources.Hotkey_ChangeHIDModeDesc;
            base.OnKeyUp = true;
            base.FontFamily = "PromptFont";
            base.Glyph = "\u243C";

            Update();

            ProfileManager.Applied += ProfileManager_Applied;
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            IsEnabled = profile.HID == HIDmode.NotSelected;
            Update();
        }

        public override void Update()
        {
            HIDmode currentHIDmode = (HIDmode)SettingsManager.GetInt(SettingsName, true);
            switch (currentHIDmode)
            {
                case HIDmode.Xbox360Controller:
                    LiveGlyph = "\uE001";
                    break;
                case HIDmode.DualShock4Controller:
                    LiveGlyph = "\uE000";
                    break;
            }

            base.Update();
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            if (IsEnabled)
            {
                HIDmode currentHIDmode = (HIDmode)SettingsManager.GetInt(SettingsName, true);
                switch (currentHIDmode)
                {
                    case HIDmode.Xbox360Controller:
                        SettingsManager.SetProperty(SettingsName, (int)HIDmode.DualShock4Controller);
                        break;
                    case HIDmode.DualShock4Controller:
                        SettingsManager.SetProperty(SettingsName, (int)HIDmode.Xbox360Controller);
                        break;
                    default:
                        break;
                }
            }
                        
            Update();
            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            HIDModeCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                FontFamily = this.FontFamily,
                Glyph = this.Glyph,
                LiveGlyph = this.LiveGlyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown,
            };

            return commands;
        }

        public override void Dispose()
        {
            ProfileManager.Applied -= ProfileManager_Applied;
            base.Dispose();
        }
    }
}
