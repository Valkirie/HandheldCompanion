using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// Custom JSON converter for LayoutTemplate.ControllerType that handles both Type objects (legacy format)
    /// and strings (new format). This ensures backward compatibility with older serialized data.
    /// 
    /// When deserializing, this converter:
    /// - Extracts the type name from legacy Type objects
    /// - Handles string values directly
    /// - Returns the simple type name (e.g., "NeptuneController")
    /// </summary>
    public class ControllerTypeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Type);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JToken token = JToken.Load(reader);

            // Handle legacy Type object format
            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                // Check if this is a .NET Type object (has $type property)
                if (obj["$type"] != null)
                {
                    // Try to deserialize as a Type
                    try
                    {
                        Type? type = serializer.Deserialize<Type>(obj.CreateReader());
                        return type;
                    }
                    catch
                    {
                        // If deserialization fails, return null
                        return null;
                    }
                }

                // Try to get FullName to recreate Type (for legacy formats)
                if (obj["FullName"] != null)
                {
                    string? fullName = obj["FullName"]?.Value<string>();
                    if (!string.IsNullOrEmpty(fullName))
                    {
                        try
                        {
                            return Type.GetType(fullName);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }

            // Don't try to deserialize strings as Type - they should go to DeviceName instead
            return null;
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
            }
            else if (value is Type type)
            {
                // When writing, serialize as a Type object for backward compatibility
                serializer.Serialize(writer, type);
            }
            else
            {
                writer.WriteNull();
            }
        }
    }
}
