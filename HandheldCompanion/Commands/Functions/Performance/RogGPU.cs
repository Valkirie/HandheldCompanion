using HandheldCompanion.Devices;
using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.Direct3D9;
using System;

namespace HandheldCompanion.Commands.Functions.Performance
{
    [Serializable]
    public class RogGPU : FunctionCommands
    {
        public RogGPU()
        {
            Name = Properties.Resources.Hotkey_XGMobile;
            Description = Properties.Resources.Hotkey_XGMobileDesc;
            FontFamily = "Segoe UI Symbol";
            Glyph = "\u2796";
            OnKeyDown = true;
            deviceType = typeof(ROGAlly);

            Update();

            ManagerFactory.deviceManager.DisplayAdapterArrived += DeviceManager_DisplayAdapterEvent;
            ManagerFactory.deviceManager.DisplayAdapterRemoved += DeviceManager_DisplayAdapterEvent;
        }

        private void DeviceManager_DisplayAdapterEvent(AdapterInformation adapterInformation)
        {
            Update();
        }

        public override bool IsToggled => IDevice.GetCurrent() is ROGAlly rOGAlly && AsusACPI.DeviceGet(AsusACPI.GPUXG) == 1;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (IDevice.GetCurrent() is ROGAlly rOGAlly)
            {
                if (!IsToggled) XGM.Reset();
                AsusACPI.SetXGMode(IsToggled);
                if (IsToggled) XGM.Init();
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public void Update(HIDmode profileMode = HIDmode.NotSelected)
        {
            IsEnabled = IDevice.GetCurrent() is ROGAlly rOGAlly && AsusACPI.IsXGConnected() == true;

            base.Update();
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

        public override void Dispose()
        {
            ManagerFactory.deviceManager.DisplayAdapterArrived -= DeviceManager_DisplayAdapterEvent;
            ManagerFactory.deviceManager.DisplayAdapterRemoved -= DeviceManager_DisplayAdapterEvent;
            base.Dispose();
        }
    }
}
