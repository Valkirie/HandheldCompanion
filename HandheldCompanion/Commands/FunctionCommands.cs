using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Multimedia;
using HandheldCompanion.Commands.Functions.Windows;
using System;
using System.Collections.Generic;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class FunctionCommands : ICommands
    {
        public static List<object> Functions = new List<object>()
        {
            "Handheld Companion",
            typeof(QuickToolsCommands),
            typeof(MainWindowCommands),
            typeof(OverlayGamepadCommands),
            typeof(OverlayTrackpadCommands),
            typeof(ChangeHIDMode),
            typeof(DesktopLayoutCommands),
            "Windows",
            typeof(OnScreenKeyboardCommands),
            typeof(OnScreenKeyboardLegacyCommands),
            typeof(KillForegroundCommands),
            typeof(ActionCenterCommands),
            typeof(SettingsCommands),
            typeof(ScreenshotCommands),
            "Multimedia",
            typeof(BrightnessIncrease),
            typeof(BrightnessDecrease),
            typeof(VolumeIncrease),
            typeof(VolumeDecrease),
        };

        public FunctionCommands()
        {
            base.commandType = CommandType.Function;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            base.Execute(IsKeyDown, IsKeyUp);
        }
    }
}
