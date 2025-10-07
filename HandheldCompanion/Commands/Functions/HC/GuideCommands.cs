using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class GuideCommands : FunctionCommands
    {
        private short KeyPressDelay = (short)(TimerManager.GetPeriod() * 2);
        public GuideCommands()
        {
            Name = Resources.Hotkey_Guide;
            Description = Resources.Hotkey_GuideDesc;
            Glyph = "\uE7FC";
            OnKeyUp = true;
        }

        public override void Execute(bool isKeyDown, bool isKeyUp, bool isBackground)
        {
            Task.Run(async () =>
            {
                ControllerManager.GetTarget()?.InjectButton(ButtonFlags.Special, true, false);
                await Task.Delay(KeyPressDelay);
                ControllerManager.GetTarget()?.InjectButton(ButtonFlags.Special, false, true);
            });
            base.Execute(isKeyDown, isKeyUp, isBackground);
        }

        public override object Clone()
        {
            GuideCommands commands = new()
            {
                commandType = commandType,
                Name = Name,
                Description = Description,
                Glyph = Glyph,
                OnKeyUp = OnKeyUp,
                OnKeyDown = OnKeyDown
            };

            return commands;
        }
    }
}