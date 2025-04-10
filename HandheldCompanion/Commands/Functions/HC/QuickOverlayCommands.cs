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

            prevDisplaylevel = ManagerFactory.settingsManager.GetInt(Settings.OnScreenDisplayLevel);

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    {
                        if (!temporary)
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
                // disable on-screen overlay
                case true:
                    ManagerFactory.settingsManager.SetProperty(SettingsName, 0, true, true);
                    break;
                // enable on-screen overlay
                case false:
                    if (prevDisplaylevel == 0)
                        prevDisplaylevel = 1;
                    ManagerFactory.settingsManager.SetProperty(SettingsName, prevDisplaylevel, true, true);
                    break;
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => ManagerFactory.settingsManager.GetInt(SettingsName, true) != 0;

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
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            base.Dispose();
        }
    }
}
