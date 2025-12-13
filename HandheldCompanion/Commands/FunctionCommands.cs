using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Commands.Functions.Multimedia;
using HandheldCompanion.Commands.Functions.Multitasking;
using HandheldCompanion.Commands.Functions.Performance;
using HandheldCompanion.Commands.Functions.Windows;
using HandheldCompanion.Properties;

using System;
using System.Collections.Generic;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class FunctionCommands : ICommands
    {
        public static List<object> Functions =
        [
            Resources.Hotkey_FunctionCategory_HandheldCompanion,
            typeof(QuickToolsCommands),
            typeof(MainWindowCommands),
            typeof(OverlayGamepadCommands),
            typeof(OverlayTrackpadCommands),
            typeof(HIDModeCommands),
            typeof(HIDStatusCommands),
            typeof(DesktopLayoutCommands),
            typeof(CycleSubProfileCommands),
            typeof(QuickOverlayCommands),
            typeof(ButtonCommands),
            Resources.Hotkey_FunctionCategory_PowerAndBattery,
            typeof(TDPIncrease),
            typeof(TDPDecrease),
            typeof(RogGPU),
            Resources.Hotkey_FunctionCategory_Windows,
            typeof(OnScreenKeyboardCommands),
            typeof(OnScreenKeyboardLegacyCommands),
            typeof(ActionCenterCommands),
            typeof(SettingsCommands),
            typeof(ScreenshotCommands),
            typeof(GameBarCommands),
            typeof(CopilotVoiceCommands),
            Resources.Hotkey_FunctionCategory_Multitasking,
            typeof(KillForegroundCommands),
            typeof(TaskManagerCommands),
            typeof(TaskViewCommands),
            typeof(SwapScreenCommands),
            typeof(DesktopCommands),
            Resources.Hotkey_FunctionCategory_Display,
            typeof(BrightnessIncrease),
            typeof(BrightnessDecrease),
            Resources.Hotkey_FunctionCategory_Sound,
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
