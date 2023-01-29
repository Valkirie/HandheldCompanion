using ControllerCommon.Managers;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ControllerCommon.Utils
{
    public static class EnumUtils
    {
        public static string GetDescriptionFromEnumValue(Enum value, string prefix = "")
        {
            // return localized string if available
            string key = string.Empty;

            if (!string.IsNullOrEmpty(prefix))
                key = $"Enum.{prefix}.{value.GetType().Name}.{value}";
            else
                key = $"Enum.{value.GetType().Name}.{value}";

            string root = Properties.Resources.ResourceManager.GetString(key);

            if (root is not null)
                return root;

            LogManager.LogError("Missing localization value for enum: {0}", key);

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
            return attribute is null ? value.ToString() : attribute.Description;
        }

        public static T GetEnumValueFromDescription<T>(string description)
        {
            var type = typeof(T);
            if (!type.IsEnum)
                throw new ArgumentException();
            FieldInfo[] fields = type.GetFields();
            var field = fields
                            .SelectMany(f => f.GetCustomAttributes(
                                typeof(DescriptionAttribute), false), (
                                    f, a) => new { Field = f, Att = a })
                            .Where(a => ((DescriptionAttribute)a.Att)
                                .Description == description).SingleOrDefault();
            return field is null ? default(T) : (T)field.Field.GetRawConstantValue();
        }
    }
}
