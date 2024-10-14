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
        public static List<object> Functions =
        [
            "Handheld Companion",
            typeof(QuickToolsCommands),
            typeof(MainWindowCommands),
            typeof(OverlayGamepadCommands),
            typeof(OverlayTrackpadCommands),
            typeof(HIDModeCommands),
            typeof(DesktopLayoutCommands),
            typeof(CycleSubProfileCommands),
            typeof(QuickOverlayCommands),
            "Windows",
            typeof(OnScreenKeyboardCommands),
            typeof(OnScreenKeyboardLegacyCommands),
            typeof(KillForegroundCommands),
            typeof(ActionCenterCommands),
            typeof(SettingsCommands),
            typeof(ScreenshotCommands),
            typeof(GameBarCommands),
            typeof(TaskManagerCommands),
            "Multimedia",
            typeof(BrightnessIncrease),
            typeof(BrightnessDecrease),
            typeof(VolumeIncrease),
            typeof(VolumeDecrease),
            typeof(VolumeMute),
        ];

        public FunctionCommands()
        {
            base.commandType = CommandType.Function;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            base.Execute(IsKeyDown, IsKeyUp, IsBackground);
        }
    }
}
