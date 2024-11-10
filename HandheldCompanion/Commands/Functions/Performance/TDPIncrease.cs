using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using System;

namespace HandheldCompanion.Commands.Functions.Performance
{
    [Serializable]
    public class TDPIncrease : FunctionCommands
    {
        public TDPIncrease()
        {
            Name = Properties.Resources.Hotkey_increaseTDP;
            Description = Properties.Resources.Hotkey_increaseTDPDesc;
            FontFamily = "Segoe UI Symbol";
            Glyph = "\u2795";
            OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            PowerProfile powerProfile = PowerProfileManager.GetCurrent();
            if (powerProfile.TDPOverrideEnabled && !powerProfile.DeviceDefault)
            {
                double TDPMax = PerformanceManager.GetMaximumTDP();
                for (int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
                    powerProfile.TDPOverrideValues[idx] = Math.Min(TDPMax, powerProfile.TDPOverrideValues[idx] + 1);

                PowerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Background);
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TDPIncrease commands = new()
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
