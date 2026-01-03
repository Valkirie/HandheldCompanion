using HandheldCompanion.Managers;
using System;
using System.Timers;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class DesktopLayoutCommands : FunctionCommands
    {
        private const string SettingsName = "LayoutMode";
        private readonly Timer ExecuteTimer;

        private bool StoredKeyDown;
        private bool StoredKeyUp;

        public DesktopLayoutCommands()
        {
            base.Name = Properties.Resources.Hotkey_LayoutMode;
            base.Description = Properties.Resources.Hotkey_LayoutModeDesc;
            base.Glyph = "\uE961";
            base.OnKeyUp = true;
            base.CanCustom = false;
            base.CanUnpin = false;

            ExecuteTimer = new(250) { AutoReset = false };
            ExecuteTimer.Elapsed += ExecuteTimer_Elapsed;

            Update();

            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case SettingsName:
                    Update();
                    // Toast notifications suppressed - handled by Controller Profile system
                    break;
            }
        }

        public override void Update()
        {
            ControllerProfile profile = (ControllerProfile)ManagerFactory.settingsManager.GetInt("ControllerProfile");
            switch (profile)
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
                    LiveGlyph = "\uE961";
                    LiveName = "Controller Profile\nDesktop";
                    break;
                case ControllerProfile.Auto:
                    LiveGlyph = "\uE9A1";
                    LiveName = "Controller Profile\nAuto";
                    break;
            }

            base.Update();
        }

        private void ExecuteTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            ControllerProfile currentProfile = (ControllerProfile)ManagerFactory.settingsManager.GetInt("ControllerProfile");

            ControllerProfile nextProfile = currentProfile switch
            {
                ControllerProfile.Native => ControllerProfile.Xbox360,
                ControllerProfile.Xbox360 => ControllerProfile.DualShock4,
                ControllerProfile.DualShock4 => ControllerProfile.Desktop,
                ControllerProfile.Desktop => ControllerProfile.Native,
                ControllerProfile.Auto => ControllerProfile.Native,
                _ => ControllerProfile.Native
            };

            ManagerFactory.settingsManager.SetProperty("ControllerProfile", (int)nextProfile);

            Update();
            base.Execute(StoredKeyDown, StoredKeyUp, false);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (ExecuteTimer.Enabled)
            {
                ExecuteTimer.Stop();
                ManagerFactory.settingsManager.SetProperty("ControllerProfile", (int)ControllerProfile.Auto);
                Update();
                base.Execute(IsKeyDown, IsKeyUp, false);
            }
            else
            {
                ExecuteTimer.Start();
                StoredKeyDown = IsKeyDown;
                StoredKeyUp = IsKeyUp;
            }
        }

        public override bool IsToggled
        {
            get
            {
                // Always toggled since we're using Controller Profiles
                return true;
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

            ExecuteTimer.Stop();
            ExecuteTimer.Dispose();

            base.Dispose();
        }
    }
}