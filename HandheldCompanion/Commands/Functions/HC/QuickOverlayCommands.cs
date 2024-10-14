using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using Microsoft.VisualBasic;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class QuickOverlayCommands : FunctionCommands
    {
        private const string SettingsName = "OnScreenDisplayLevel";

        public bool _IsToggled = false;
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
                    if (!temporary)
                        prevDisplaylevel = Convert.ToInt16(value);
                    break;
            }
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            switch(_IsToggled)
            {
                case true:
                    SettingsManager.SetProperty(SettingsName, prevDisplaylevel, false, true);
                    break;
                case false:
                    SettingsManager.SetProperty(SettingsName, 0, false, true);
                    break;
            }

            // invert toggle
            _IsToggled = !_IsToggled;

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override bool IsToggled => !_IsToggled;

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
