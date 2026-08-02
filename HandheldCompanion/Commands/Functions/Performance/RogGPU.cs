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

        private static bool IsSupported
        {
            get
            {
                IDevice device = IDevice.GetCurrent();

                if (!device.ManufacturerName.Equals("ASUSTEK COMPUTER INC.", StringComparison.OrdinalIgnoreCase) ||
                    IncompatibleAsusProducts.Contains(device.ProductName))
                    return false;

                if (device.Capabilities.HasFlag(DeviceCapabilities.XGMobile))
                    return AsusACPI.Open();

                // The XG Mobile ACPI endpoint is present on every compatible ASUS host,
                // including Flow laptops which currently use DefaultDevice in HC.
                bool isSupported = AsusACPI.Open() && AsusACPI.IsSupported(AsusACPI.GPUXG);
                if (isSupported)
                    device.Capabilities |= DeviceCapabilities.XGMobile;

                return isSupported;
            }
        }

        public override bool IsToggled => IsSupported && AsusACPI.DeviceGet(AsusACPI.GPUXG) == 1;

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (IsSupported)
            {
                bool enable = !IsToggled;

                if (!enable) XGM.Reset();
                AsusACPI.SetXGMode(enable);
                if (enable) XGM.Init();
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public void Update(HIDmode profileMode = HIDmode.NotSelected)
        {
            IsEnabled = IsSupported;

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
