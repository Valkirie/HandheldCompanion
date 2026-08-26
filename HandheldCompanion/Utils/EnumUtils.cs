using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace HandheldCompanion.Utils;

public static class EnumUtils
{
    private static HashSet<string> missingKeys = new();
    public static string GetDescriptionFromEnumValue(Enum value, string prefix = "", string suffix = "")
    {
        // return localized string if available
        string key;

        if (!string.IsNullOrEmpty(prefix))
            key = $"Enum_{prefix}_{value.GetType().Name}_{value}";
        else if (!string.IsNullOrEmpty(suffix))
            key = $"Enum_{value.GetType().Name}_{value}_{suffix}";
        else
            key = $"Enum_{value.GetType().Name}_{value}";

        string? root = Resources.ResourceManager.GetString(key);
        if (!string.IsNullOrEmpty(root) && !root.Equals(key))
            return root;

        // return description otherwise
        DescriptionAttribute? attribute = null;

        try
        {
            attribute = value.GetType()
                        .GetField(value.ToString())?
                        .GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .SingleOrDefault() as DescriptionAttribute;
        }
        catch { }

        if (attribute is not null)
            return attribute.Description;

        // only display enum warnings once
        if (missingKeys.Add(key))
            LogManager.LogTrace("No localization for enum: {0}", key);

        return value.ToString();
    }
}