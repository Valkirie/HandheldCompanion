using System;
using System.Threading;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

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

        private IVirtualGamepad PressPsButton()
        {
            var controller = VirtualManager.vClient.CreateDualShock4Controller();
            controller.Connect();
            Thread.Sleep(100);
            controller.SetButtonState(DualShock4SpecialButton.Ps, true);
            Thread.Sleep(100);
            controller.SetButtonState(DualShock4SpecialButton.Ps, false);
            return controller;
        }

        private IVirtualGamepad PressGuideButton()
        {
            var controller = VirtualManager.vClient.CreateXbox360Controller();
            controller.Connect();
            Thread.Sleep(100);
            controller.SetButtonState(Xbox360Button.Guide, true);
            Thread.Sleep(100);
            controller.SetButtonState(Xbox360Button.Guide, false);
            return controller;
        }

        public override void Execute(bool isKeyDown, bool isKeyUp, bool isBackground)
        {
            base.Execute(isKeyDown, isKeyUp, isBackground);

            var state = VirtualManager.vTarget.IsConnected;
            var profile = ManagerFactory.profileManager.GetCurrent();

            VirtualManager.vTarget.Disconnect();

            var controller = profile.HID == HIDmode.DualShock4Controller ? PressPsButton() : PressGuideButton();
            
            controller.Disconnect();
            
            if (state) VirtualManager.vTarget.Connect();
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