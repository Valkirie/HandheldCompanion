using System;
using Microsoft.Win32;

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
}