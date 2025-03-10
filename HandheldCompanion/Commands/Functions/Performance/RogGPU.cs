using HandheldCompanion.Devices;
using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using System;

namespace HandheldCompanion.Commands.Functions.Performance
{
    [Serializable]
    public class RogGPU : FunctionCommands
    {
        public RogGPU()
        {
            Name = "XG Mobile";
            Description = "Enable/disable XG Mobile";
            FontFamily = "Segoe UI Symbol";
            Glyph = "\u2796";
            OnKeyDown = true;
            deviceType = typeof(ROGAlly);
        }

        public override bool IsToggled => IDevice.GetCurrent() is ROGAlly rOGAlly && rOGAlly.asusACPI?.DeviceGet(AsusACPI.GPUEco) == 1;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (IDevice.GetCurrent() is ROGAlly rOGAlly)
            {
                rOGAlly.asusACPI?.SetGPUEco(IsToggled ? 0 : 1);
                switch (IsToggled)
                {
                    case true:
                        rOGAlly.asusACPI?.SetGPUEco(0);
                        break;
                    case false:
                        rOGAlly.asusACPI?.SetGPUEco(1);
                        break;
                }
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            RogGPU commands = new()
            {
                commandType = commandType,
                Name = Name,
                Description = Description,
                FontFamily = FontFamily,
                Glyph = Glyph,
                OnKeyUp = OnKeyUp,
                OnKeyDown = OnKeyDown
            };

            return commands;
        }
    }
}
