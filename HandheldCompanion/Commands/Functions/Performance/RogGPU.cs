using HandheldCompanion.Devices;
using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;

namespace HandheldCompanion.Commands.Functions.Performance
{
    [Serializable]
    public class RogGPU : FunctionCommands
    {
        private static bool? xgMobileSupported;
        private bool isToggled;

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

        private static readonly HashSet<string> IncompatibleAsusProducts = new(StringComparer.OrdinalIgnoreCase)
        {
            "RC72LA", // ROG Ally X
            "RC73YA", // ROG Xbox Ally
            "RC73XA", // ROG Xbox Ally X
        };

        private static bool IsSupported()
        {
            if (xgMobileSupported.HasValue)
                return xgMobileSupported.Value && AsusACPI.Open();

            IDevice device = IDevice.GetCurrent();

            if (!device.ManufacturerName.Equals("ASUSTEK COMPUTER INC.", StringComparison.OrdinalIgnoreCase) ||
                IncompatibleAsusProducts.Contains(device.ProductName))
            {
                xgMobileSupported = false;
                return false;
            }

            if (device.Capabilities.HasFlag(DeviceCapabilities.XGMobile))
            {
                xgMobileSupported = true;
                return AsusACPI.Open();
            }

            if (!AsusACPI.Open())
                return false;

            // The XG Mobile ACPI endpoint is present on every compatible ASUS host,
            // including Flow laptops which currently use DefaultDevice in HC.
            xgMobileSupported = AsusACPI.IsSupported(AsusACPI.GPUXG);
            if (xgMobileSupported.Value)
                device.Capabilities |= DeviceCapabilities.XGMobile;

            return xgMobileSupported.Value;
        }

        private static bool TryGetState(out bool toggled)
        {
            toggled = false;
            if (!IsSupported())
                return false;

            int state = AsusACPI.DeviceGet(AsusACPI.GPUXG);
            if (state < 0)
                return false;

            toggled = state == 1;
            return true;
        }

        public override bool IsToggled => isToggled;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (TryGetState(out bool toggled))
            {
                bool enable = !toggled;

                if (!enable) XGM.Reset();
                if (AsusACPI.DeviceSet(AsusACPI.GPUXG, enable ? 1 : 0) == 0)
                    isToggled = enable;
                if (enable) XGM.Init();
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public void Update(HIDmode profileMode = HIDmode.NotSelected)
        {
            IsEnabled = TryGetState(out bool toggled);
            isToggled = IsEnabled && toggled;

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
