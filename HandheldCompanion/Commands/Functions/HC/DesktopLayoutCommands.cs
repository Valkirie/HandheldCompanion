using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class DesktopLayoutCommands : FunctionCommands
    {
        private const string SettingsName = "DesktopLayoutEnabled";

        public DesktopLayoutCommands()
        {
            base.Name = Properties.Resources.Hotkey_DesktopLayoutEnabled;
            base.Description = Properties.Resources.Hotkey_DesktopLayoutEnabledDesc;
            base.Glyph = "\uE961";
            base.OnKeyUp = true;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case SettingsName:
                    base.Execute(OnKeyDown, OnKeyUp);
                    break;
            }
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            bool value = !SettingsManager.GetBoolean(SettingsName, true);
            SettingsManager.SetProperty(SettingsName, value, false, true);

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override bool IsToggled => SettingsManager.GetBoolean(SettingsName, true);

        public override object Clone()
        {
            DesktopLayoutCommands commands = new()
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
