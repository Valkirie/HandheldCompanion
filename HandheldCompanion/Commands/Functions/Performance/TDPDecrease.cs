using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using System;

namespace HandheldCompanion.Commands.Functions.Performance
{
    [Serializable]
    public class TDPDecrease : FunctionCommands
    {
        public TDPDecrease()
        {
            Name = Properties.Resources.Hotkey_decreaseTDP;
            Description = Properties.Resources.Hotkey_decreaseTDPDesc;
            FontFamily = "Segoe UI Symbol";
            Glyph = "\u2796";
            OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            PowerProfile powerProfile = ManagerFactory.powerProfileManager.GetCurrent();
            if (powerProfile.TDPOverrideEnabled && !powerProfile.DeviceDefault)
            {
                double TPDMin = PerformanceManager.GetMinimumTDP();
                for (int idx = (int)PowerType.Slow; idx <= (int)PowerType.Fast; idx++)
                    powerProfile.TDPOverrideValues[idx] = Math.Max(TPDMin, powerProfile.TDPOverrideValues[idx] - 1);

                ManagerFactory.powerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Background);
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TDPDecrease commands = new()
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
