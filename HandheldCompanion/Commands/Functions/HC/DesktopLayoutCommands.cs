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

        private void ExecuteTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Get the current value of LayoutMode
            int value = ManagerFactory.settingsManager.GetInt(SettingsName);
            LayoutModes layoutMode = (LayoutModes)value;

            // Increment or reset the value based on its current state
            switch (layoutMode)
            {
                case LayoutModes.Gamepad:
                    layoutMode = LayoutModes.Desktop;
                    break;
                default:
                case LayoutModes.Desktop:
                    layoutMode = LayoutModes.Gamepad;
                    break;
            }

            // Update settings
            ManagerFactory.settingsManager.SetProperty(SettingsName, (int)layoutMode);

            Update();
            base.Execute(StoredKeyDown, StoredKeyUp, false);
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (ExecuteTimer.Enabled)
            {
                // Timer is already running, it's likely a double press
                ExecuteTimer.Stop();

                // Update settings
                ManagerFactory.settingsManager.SetProperty(SettingsName, (int)LayoutModes.Auto);

                Update();
                base.Execute(IsKeyDown, IsKeyUp, false);
            }
            else
            {
                // Start the timer and store the key states
                ExecuteTimer.Start();

                StoredKeyDown = IsKeyDown;
                StoredKeyUp = IsKeyUp;
            }
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

            ExecuteTimer.Stop();
            ExecuteTimer.Dispose();

            base.Dispose();
        }
    }
}
