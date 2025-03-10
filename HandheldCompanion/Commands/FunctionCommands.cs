using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Multimedia;
using HandheldCompanion.Commands.Functions.Multitasking;
using HandheldCompanion.Commands.Functions.Performance;
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
            "Power & battery",
            typeof(TDPIncrease),
            typeof(TDPDecrease),
            typeof(RogGPU),
            "Windows",
            typeof(OnScreenKeyboardCommands),
            typeof(OnScreenKeyboardLegacyCommands),
            typeof(ActionCenterCommands),
            typeof(SettingsCommands),
            typeof(ScreenshotCommands),
            typeof(GameBarCommands),
            "Multitasking",
            typeof(KillForegroundCommands),
            typeof(TaskManagerCommands),
            typeof(SwapScreenCommands),
            typeof(DesktopCommands),
            "Display",
            typeof(BrightnessIncrease),
            typeof(BrightnessDecrease),
            "Sound",
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
