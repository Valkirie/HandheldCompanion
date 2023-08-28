using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using System;
using System.ComponentModel;
using System.Linq;

namespace HandheldCompanion.Utils;

public static class EnumUtils
{
    public static string GetDescriptionFromEnumValue(Enum value, string prefix = "")
    {
        // return localized string if available
        var key = string.Empty;

        if (!string.IsNullOrEmpty(prefix))
            key = $"Enum.{prefix}.{value.GetType().Name}.{value}";
        else
            key = $"Enum.{value.GetType().Name}.{value}";

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

        LogManager.LogError("Neither localization nor description exists for enum: {0}", key);
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