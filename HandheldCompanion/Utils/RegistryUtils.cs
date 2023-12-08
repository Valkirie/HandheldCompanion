using Microsoft.Win32;
using System;

namespace HandheldCompanion.Utils;

public static class RegistryUtils
{
    private const string HKLM = @"HKEY_LOCAL_MACHINE";

<<<<<<< HEAD
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

    public static bool KeyExists(string key, string valueName)
    {
        var keyName = HKLM + "\\" + key;
        return Registry.GetValue(keyName, valueName, null) != null;
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
=======
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
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        return 0;
    }

<<<<<<< HEAD
    public static bool GetBoolean(string key, string valueName)
    {
        try
        {
            return Convert.ToBoolean(GetValue(key, valueName));
        }
        catch { }
=======
    public static bool GetBoolean(string key, string value)
    {
        try
        {
            return Convert.ToBoolean(GetValue(key, value));
        }
        catch
        {
        }
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        return false;
    }

<<<<<<< HEAD
    public static bool SearchForKeyValue(string key, string valueName, string value)
=======
    public static bool SearchForKeyValue(string key, string value, string content)
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
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
<<<<<<< HEAD
                if (subKey != null && subKey.GetValue(valueName) != null)
                {
                    // Get the value of DeviceDesc as a string
                    string subKeyDesc = subKey.GetValue(valueName).ToString();

                    // Check if the value contains the target strings
                    if (subKeyDesc.Contains(value, StringComparison.InvariantCultureIgnoreCase))
=======
                if (subKey != null && subKey.GetValue(value) != null)
                {
                    // Get the value of DeviceDesc as a string
                    string subKeyDesc = subKey.GetValue(value).ToString();

                    // Check if the value contains the target strings
                    if (subKeyDesc.Contains(content, StringComparison.InvariantCultureIgnoreCase))
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
                        return true;
                }
            }
        }

        return false;
    }
}