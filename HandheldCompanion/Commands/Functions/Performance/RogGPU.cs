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

            Update();

            ManagerFactory.deviceManager.DisplayAdapterArrived += DeviceManager_DisplayAdapterEvent;
            ManagerFactory.deviceManager.DisplayAdapterRemoved += DeviceManager_DisplayAdapterEvent;
        }

        private void DeviceManager_DisplayAdapterEvent(AdapterInformation adapterInformation)
        {
            Update();
        }

        private static bool IsSupported()
        {
            return IDevice.GetCurrent().Capabilities.HasFlag(DeviceCapabilities.XGMobile);
        }

        private static bool TryGetState(out bool toggled)
        {
            toggled = false;
            if (!IsSupported())
                return false;

            bool? state = AsusACPI.GetXGMode();
            if (state is null)
                return false;

            toggled = state.Value;
            return true;
        }

        public override bool IsToggled => AsusACPI.GetXGMode() == true;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (TryGetState(out bool toggled))
            {
                bool enable = !toggled;

                if (!enable) XGM.Reset();
                if (AsusACPI.SetXGMode(enable) && enable) XGM.Init();
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public void Update(HIDmode profileMode = HIDmode.NotSelected)
        {
            IsEnabled = TryGetState(out _);

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
