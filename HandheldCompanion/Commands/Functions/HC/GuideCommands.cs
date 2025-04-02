using System;
using System.Threading;
using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;

namespace HandheldCompanion.Commands.Functions.HC
{
    [Serializable]
    public class GuideCommands : FunctionCommands
    {
        public GuideCommands()
        {
            Name = Resources.Hotkey_Guide;
            Description = Resources.Hotkey_GuideDesc;
            Glyph = "\uE7FC";
            OnKeyDown = true;
        }

        private void SetGuideButtonState(bool pressed)
        {
            VirtualManager.UpdateInputs(
                new ControllerState()
                {
                    ButtonState = new ButtonState()
                    {
                        [ButtonFlags.Special] = pressed
                    }
                }
            );
        }

        private void PressGuideButton()
        {
            SetGuideButtonState(true);
            Thread.Sleep(10);
            SetGuideButtonState(false);
        }

        public override void Execute(bool isKeyDown, bool isKeyUp, bool isBackground)
        {
            if (!VirtualManager.vTarget.IsConnected) return;
            PressGuideButton();
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