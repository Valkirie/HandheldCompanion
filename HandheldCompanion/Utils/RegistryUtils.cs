using Microsoft.Win32;
using System;

namespace HandheldCompanion.Utils;

public static class RegistryUtils
{
    private const string HKLM = @"HKEY_LOCAL_MACHINE";

    private static object GetValue(string key, string value)
    {
        var keyName = HKLM + "\\" + key;
        return Registry.GetValue(keyName, value, null);
    }

    public static string GetString(string key, string value)
    {
        return Convert.ToString(GetValue(key, value));
    }

    public static int GetInt(string key, string value)
    {
        try
        {
            return Convert.ToInt32(GetValue(key, value));
        }
        catch
        {
        }

        return 0;
    }

    public static bool GetBoolean(string key, string value)
    {
        try
        {
            return Convert.ToBoolean(GetValue(key, value));
        }
        catch
        {
        }

        return false;
    }

    public static bool SearchForKeyValue(string key, string value, string content)
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
                if (subKey != null && subKey.GetValue(value) != null)
                {
                    // Get the value of DeviceDesc as a string
                    string subKeyDesc = subKey.GetValue(value).ToString();

                    // Check if the value contains the target strings
                    if (subKeyDesc.Contains(content, StringComparison.InvariantCultureIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }
}