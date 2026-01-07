using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class HIDModeCommands : FunctionCommands
    {
        private const string SettingsName = "ControllerProfile"; // ← CHANGED from "HIDmode"

        public HIDModeCommands()
        {
            base.Name = Properties.Resources.Hotkey_ChangeHIDMode;
            base.Description = Properties.Resources.Hotkey_ChangeHIDModeDesc;
            base.OnKeyUp = true;
            base.FontFamily = "PromptFont";
            base.Glyph = "\u243C";

            Update();

            // ← ADD THIS EVENT SUBSCRIPTION
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
        }

        // ← ADD THIS METHOD
        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    Update();

                    ControllerProfile profile = (ControllerProfile)ManagerFactory.settingsManager.GetInt(SettingsName);
                    ToastManager.SendToast($"Controller Profile", $"{profile}", $"controller_profile_{(int)profile}", true);
                    break;
            }
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            IsEnabled = true;
            Update();
        }

        public void Update(HIDmode profileMode = HIDmode.NotSelected)
        {
            ControllerProfile currentProfile = (ControllerProfile)ManagerFactory.settingsManager.GetInt("ControllerProfile"); // ← Remove the 'true'

            switch (currentProfile)
            {
                case ControllerProfile.Native:
                    LiveGlyph = "\u243C";
                    LiveName = "Controller Profile\nNative";
                    break;
                case ControllerProfile.Xbox360:
                    LiveGlyph = "\uE001";
                    LiveName = "Controller Profile\nXbox 360";
                    break;
                case ControllerProfile.DualShock4:
                    LiveGlyph = "\uE000";
                    LiveName = "Controller Profile\nDualShock 4";
                    break;
                case ControllerProfile.Desktop:
                    LiveGlyph = "\uE75A";
                    LiveName = "Controller Profile\nDesktop";
                    break;
                case ControllerProfile.Auto:
                    LiveGlyph = "\uE8B7";
                    LiveName = "Controller Profile\nAuto";
                    break;
            }

            base.Update();
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            ControllerProfile currentProfile = (ControllerProfile)ManagerFactory.settingsManager.GetInt("ControllerProfile"); // ← Remove the 'true'

            ControllerProfile nextProfile = currentProfile switch
            {
                ControllerProfile.Native => ControllerProfile.Xbox360,
                ControllerProfile.Xbox360 => ControllerProfile.DualShock4,
                ControllerProfile.DualShock4 => ControllerProfile.Desktop,
                ControllerProfile.Desktop => ControllerProfile.Auto,
                ControllerProfile.Auto => ControllerProfile.Native,
                _ => ControllerProfile.Native
            };

            ManagerFactory.settingsManager.SetProperty(SettingsName, (int)nextProfile); // ← Use SettingsName

            Update();
            base.Execute(IsKeyDown, IsKeyUp, false);
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
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged; // ← ADD THIS
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            base.Dispose();
        }
    }
}
