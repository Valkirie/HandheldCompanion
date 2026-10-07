using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class QuickOverlayCommands : FunctionCommands
    {
        private const string SettingsName = "OnScreenDisplayLevel";
        private const string LastSettingsName = "LastOnScreenDisplayLevel";

        public QuickOverlayCommands()
        {
            base.Name = Properties.Resources.Hotkey_OnScreenDisplayToggle;
            base.Description = Properties.Resources.Hotkey_OnScreenDisplayToggleDesc;
            base.Glyph = "\uE78B";
            base.OnKeyUp = true;

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case SettingsName:
                    Update();
                    break;
            }
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            switch (IsToggled)
            {
                // disable on-screen overlay
                case true:
                    ManagerFactory.settingsManager.SetProperty(SettingsName, 0);
                    break;
                // enable on-screen overlay
                case false:
                    int restore = ManagerFactory.settingsManager.GetInt(LastSettingsName);
                    if (restore == 0)
                        restore = 1;
                    ManagerFactory.settingsManager.SetProperty(SettingsName, restore);
                    break;
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled => ManagerFactory.settingsManager.GetInt(SettingsName) != 0;

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
