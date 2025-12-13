using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class HIDStatusCommands : FunctionCommands
    {
        private const string SettingsName = "HIDstatus";

        public HIDStatusCommands()
        {
            base.Name = Properties.Resources.Hotkey_ChangeHIDStatus;
            base.Description = Properties.Resources.Hotkey_ChangeHIDStatusDesc;
            base.OnKeyUp = true;
            base.Glyph = "\ue7fc";

            Update();

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    Update();
                    break;
            }
        }

        public override bool IsToggled => (HIDstatus)ManagerFactory.settingsManager.GetInt(SettingsName, true) == HIDstatus.Connected;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (IsEnabled)
            {
                HIDstatus currentHIDstatus = (HIDstatus)ManagerFactory.settingsManager.GetInt(SettingsName, true);
                switch (currentHIDstatus)
                {
                    case HIDstatus.Connected:
                        ManagerFactory.settingsManager.SetProperty(SettingsName, (int)HIDstatus.Disconnected);
                        break;
                    case HIDstatus.Disconnected:
                        ManagerFactory.settingsManager.SetProperty(SettingsName, (int)HIDstatus.Connected);
                        break;
                }
            }

            Update();
            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            HIDStatusCommands commands = new()
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
    }
}
