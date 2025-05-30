using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class DesktopLayoutCommands : FunctionCommands
    {
        private const string SettingsName = "LayoutMode";

        public DesktopLayoutCommands()
        {
            base.Name = Properties.Resources.Hotkey_LayoutMode;
            base.Description = Properties.Resources.Hotkey_LayoutModeDesc;
            base.Glyph = "\uE961";
            base.OnKeyUp = true;
            base.CanCustom = false;
            base.CanUnpin = false;

            Update();

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    Update();

                    LayoutModes LayoutMode = (LayoutModes)ManagerFactory.settingsManager.GetInt(SettingsName);
                    ToastManager.SendToast($"Controller mode set to {LayoutMode}");
                    break;
            }
        }

        public override void Update()
        {
            LayoutModes LayoutMode = (LayoutModes)ManagerFactory.settingsManager.GetInt(SettingsName);
            switch (LayoutMode)
            {
                case LayoutModes.Gamepad:
                    LiveGlyph = "\uE7FC";
                    LiveName = "Controller mode\nGamepad";
                    break;
                case LayoutModes.Desktop:
                    LiveGlyph = "\uE961";
                    LiveName = "Controller mode\nDesktop";
                    break;
                case LayoutModes.Auto:
                    LiveGlyph = "\uE9A1";
                    LiveName = "Controller mode\nAuto";
                    break;
            }

            base.Update();
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            // Get the current value of LayoutMode
            int LayoutMode = ManagerFactory.settingsManager.GetInt(SettingsName);

            // Increment or reset the value based on its current state
            LayoutMode = (LayoutMode == (int)LayoutModes.Auto) ? (int)LayoutModes.Gamepad : LayoutMode + 1;

            // Update settings
            ManagerFactory.settingsManager.SetProperty(SettingsName, LayoutMode);

            Update();
            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override bool IsToggled
        {
            get
            {
                LayoutModes LayoutMode = (LayoutModes)ManagerFactory.settingsManager.GetInt(SettingsName);
                switch (LayoutMode)
                {
                    case LayoutModes.Gamepad:
                    case LayoutModes.Desktop:
                        return true;
                    default:
                        return false;
                }
            }
        }

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
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            base.Dispose();
        }
    }
}
