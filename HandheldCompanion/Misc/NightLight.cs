using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Misc
{
    public static class NightLight
    {
        private static string _key =
            "Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\Store\\DefaultAccount\\Current\\default$windows.data.bluelightreduction.bluelightreductionstate\\windows.data.bluelightreduction.bluelightreductionstate";

        private static RegistryKey _registryKey;

        private static readonly RegistryWatcher _nightLightStateWatcher = new(WatchedRegistry.CurrentUser,
        @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\" +
        @"Store\\DefaultAccount\\Current\\default$windows.data.bluelightreduction.bluelightreductionstate\\" +
        @"windows.data.bluelightreduction.bluelightreductionstate", "Data");

        public static bool Enabled
        {
            get
            {
                if (!Supported) return false;

                byte[] data = new byte[43];
                byte[] registry = _registryKey?.GetValue("Data") as byte[];
                Array.Copy(registry, 0, data, 0, registry.Length); // copy the second array into the first array starting at index 5                
                if (data == null) return false;
                return data[18] == 0x15;
            }
            set
            {
                if (Supported && Enabled != value) Toggle();
            }
        }

        public static bool Supported
        {
            get => _registryKey != null;
        }

        static NightLight()
        {
            _registryKey = Registry.CurrentUser.OpenSubKey(_key, true);

            if (!Supported)
                return;

            _nightLightStateWatcher.RegistryChanged += UpdateNightLight;
            _nightLightStateWatcher.StartWatching();
        }

        private static void UpdateNightLight(object? sender, RegistryChangedEventArgs e)
        {
            Toggled?.Invoke(Enabled);
        }

        public static event ToggledEventHandler Toggled;
        public delegate void ToggledEventHandler(bool enabled);

        private static void Toggle()
        {
            byte[] data = new byte[43];
            byte[] newData = new byte[43];
            byte[] registry = _registryKey?.GetValue("Data") as byte[];
            Array.Copy(registry, 0, data, 0, registry.Length); // copy the second array into the first array starting at index 5

            if (Enabled)
            {
                newData = new byte[41];
                Array.Copy(data, 0, newData, 0, 22);
                Array.Copy(data, 25, newData, 23, 43 - 25);
                newData[18] = 0x13;
            }
            else
            {
                Array.Copy(data, 0, newData, 0, 22);
                Array.Copy(data, 23, newData, 25, 41 - 23);
                newData[18] = 0x15;
                newData[23] = 0x10;
                newData[24] = 0x00;
            }

            for (int i = 10; i < 15; i++)
            {
                if (newData[i] != 0xff)
                {
                    newData[i]++;
                    break;
                }
            }

            _registryKey.SetValue("Data", newData, RegistryValueKind.Binary);
            _registryKey.Flush();
        }
    }
}
