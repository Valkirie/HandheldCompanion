using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class ButtonCommands : FunctionCommands
    {
        public short KeyPressDelay = (short)(TimerManager.GetPeriod() * 2);
        public ButtonFlags ButtonFlags = ButtonFlags.Special;

        public ButtonCommands()
        {
            Name = Resources.Hotkey_ButtonCommands;
            Description = Resources.Hotkey_ButtonCommandsDesc;
            Glyph = "\uE7FC";
            OnKeyUp = true;
        }

        public override void Execute(bool isKeyDown, bool isKeyUp, bool isBackground)
        {
            Task.Run(async () =>
            {
                ControllerManager.GetTarget()?.InjectButton(ButtonFlags, true, false);
                await Task.Delay(KeyPressDelay);
                ControllerManager.GetTarget()?.InjectButton(ButtonFlags, false, true);
            });
            base.Execute(isKeyDown, isKeyUp, isBackground);
        }

        public override object Clone()
        {
            ButtonCommands commands = new()
            {
                commandType = commandType,
                Name = Name,
                Description = Description,
                Glyph = Glyph,
                OnKeyUp = OnKeyUp,
                OnKeyDown = OnKeyDown,
                KeyPressDelay = KeyPressDelay,
                ButtonFlags = ButtonFlags,
            };

            return commands;
        }
    }
}