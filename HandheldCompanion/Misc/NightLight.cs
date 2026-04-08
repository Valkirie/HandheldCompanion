using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;

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

                byte[] registry = _registryKey?.GetValue("Data") as byte[];
                if (registry == null || registry.Length < 19) return false;

                byte[] data = new byte[registry.Length];
                Array.Copy(registry, 0, data, 0, registry.Length);
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

            // If registry key doesn't exist, try to create it with default values
            if (_registryKey == null)
            {
                try
                {
                    // Try to create the parent key structure
                    string parentKey = _key.Substring(0, _key.LastIndexOf('\\'));
                    using (var parent = Registry.CurrentUser.CreateSubKey(parentKey))
                    {
                        if (parent != null)
                        {
                            // Open the key with write access
                            _registryKey = Registry.CurrentUser.OpenSubKey(_key, true);

                            // If still null, create the full path
                            if (_registryKey == null)
                            {
                                _registryKey = Registry.CurrentUser.CreateSubKey(_key);
                            }

                            // Initialize with default Night Light disabled state
                            if (_registryKey != null)
                            {
                                InitializeDefaultData();
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // If we can't create the key, Night Light won't be supported
                    _registryKey = null;
                }
            }

            if (!Supported)
                return;

            _nightLightStateWatcher.RegistryChanged += UpdateNightLight;
            _nightLightStateWatcher.StartWatching();
        }

        private static void InitializeDefaultData()
        {
            // Default Night Light disabled state
            // This is the minimal binary data structure for a disabled Night Light state
            byte[] defaultData = new byte[41]
            {
                0x43, 0x42, 0x01, 0x00, 0x0A, 0xCA, 0x14, 0x5B, 0xB0, 0x10, 0xE5, 0x01, 0xD2, 0x8C, 0x01, 0x00,
                0x00, 0x00, 0x13, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            try
            {
                _registryKey.SetValue("Data", defaultData, RegistryValueKind.Binary);
                _registryKey.Flush();
            }
            catch (Exception)
            {
                // If we can't write the default data, the feature won't work properly
            }
        }

        private static void UpdateNightLight(object? sender, RegistryChangedEventArgs e)
        {
            Toggled?.Invoke(Enabled);
        }

        public static event ToggledEventHandler Toggled;
        public delegate void ToggledEventHandler(bool enabled);

        private static void Toggle()
        {
            byte[] registry = _registryKey?.GetValue("Data") as byte[];
            if (registry == null || registry.Length < 23)
            {
                // If registry data is invalid or incomplete, initialize with defaults
                InitializeDefaultData();
                registry = _registryKey?.GetValue("Data") as byte[];
                if (registry == null)
                    return;
            }

            byte[] data = new byte[registry.Length];
            byte[] newData = new byte[43];

            Array.Copy(registry, 0, data, 0, registry.Length);

            if (Enabled)
            {
                newData = new byte[41];
                Array.Copy(data, 0, newData, 0, 22);
                if (data.Length >= 43)
                    Array.Copy(data, 25, newData, 23, 43 - 25);
                newData[18] = 0x13;
            }
            else
            {
                Array.Copy(data, 0, newData, 0, 22);
                if (data.Length >= 41)
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
