using Microsoft.Win32;
using System;

namespace HandheldCompanion.Utils;

public static class RegistryUtils
{
    private const string HKLM = @"HKEY_LOCAL_MACHINE";

    private static object GetValue(string key, string valueName)
    {
        var keyName = HKLM + "\\" + key;
        return Registry.GetValue(keyName, valueName, null);
    }

    public static void SetValue(string key, string valueName, object value)
    {
        var keyName = HKLM + "\\" + key;
        Registry.SetValue(keyName, valueName, value);
    }

    public static string GetString(string key, string valueName)
    {
        return Convert.ToString(GetValue(key, valueName));
    }

    public static int GetInt(string key, string valueName)
    {
        try
        {
            return Convert.ToInt32(GetValue(key, valueName));
        }
        catch { }

        return 0;
    }

    public static bool GetBoolean(string key, string valueName)
    {
        try
        {
            return Convert.ToBoolean(GetValue(key, valueName));
        }
        catch { }

        return false;
    }

    public static bool SearchForKeyValue(string key, string valueName, string value)
    {
        // Open the root registry key for reading
        RegistryKey root = Registry.LocalMachine.OpenSubKey(key);

        // Check if the root key exists
        if (root != null)
        {
            // Get the names of the subkeys under the root key
            string[] subkeys = root.GetSubKeyNames();

            // Loop through each subkey
            foreach (string subkey in subkeys)
            {
                // Open the subkey for reading
                RegistryKey subKey = root.OpenSubKey(subkey);

                // Check if the subkey exists and has a value named DeviceDesc
                if (subKey != null && subKey.GetValue(valueName) != null)
                {
                    // Get the value of DeviceDesc as a string
                    string subKeyDesc = subKey.GetValue(valueName).ToString();

                    // Check if the value contains the target strings
                    if (subKeyDesc.Contains(value, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }
}