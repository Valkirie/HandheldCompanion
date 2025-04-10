﻿using HandheldCompanion.Properties;
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

        var root = Resources.ResourceManager.GetString(key);

        if (root is not null)
            return root;

        // return description otherwise
        DescriptionAttribute attribute = null;

        try
        {
            attribute = value.GetType()
                        .GetField(value.ToString())
                        .GetCustomAttributes(typeof(DescriptionAttribute), false)
                        .SingleOrDefault() as DescriptionAttribute;
        }
        catch { }

        if (attribute is not null)
            return attribute.Description;

        // only display enum warnings once
        if (missingKeys.Add(key))
            LogManager.LogWarning("No localization for enum: {0}", key);

        return value.ToString();
    }

    public static T GetEnumValueFromDescription<T>(string description)
    {
        var type = typeof(T);
        if (!type.IsEnum)
            throw new ArgumentException();
        var fields = type.GetFields();
        var field = fields
            .SelectMany(f => f.GetCustomAttributes(
                typeof(DescriptionAttribute), false), (
                f, a) => new { Field = f, Att = a }).SingleOrDefault(a => ((DescriptionAttribute)a.Att)
                .Description == description);
        return field is null ? default : (T)field.Field.GetRawConstantValue();
    }
}