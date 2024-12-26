using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class QuickOverlayCommands : FunctionCommands
    {
        private const string SettingsName = "OnScreenDisplayLevel";
        private int prevDisplaylevel = 0;

        public QuickOverlayCommands()
        {
            base.Name = Properties.Resources.Hotkey_OnScreenDisplayToggle;
            base.Description = Properties.Resources.Hotkey_OnScreenDisplayToggleDesc;
            base.Glyph = "\uE78B";
            base.OnKeyUp = true;

            prevDisplaylevel = SettingsManager.GetInt(Settings.OnScreenDisplayLevel);

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    {
                        prevDisplaylevel = Convert.ToInt16(value);
                        Update();
                    }
                    break;
            }
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            switch (IsToggled)
            {
                case true:
                    SettingsManager.SetProperty(SettingsName, 0, false);
                    break;
                case false:
                    if (prevDisplaylevel == 0)
                        prevDisplaylevel = 1;
                    SettingsManager.SetProperty(SettingsName, prevDisplaylevel, false);
                    break;
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => SettingsManager.GetInt(SettingsName) != 0;

        public override object Clone()
        {
            QuickOverlayCommands commands = new()
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

        public override void Dispose()
        {
            SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            base.Dispose();
        }
    }
}
