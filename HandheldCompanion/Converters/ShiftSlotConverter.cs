using HandheldCompanion.Actions;
using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Converters;

/// <summary>
/// JSON converter that handles migration of old ShiftSlot values (sequential 0-5) 
/// to new [Flags] values (powers of 2).
/// Old format: integers - None=0, ShiftA=1, ShiftB=2, ShiftC=3, ShiftD=4, Any=5
/// New format: strings - "None", "ShiftA", "ShiftB", "ShiftC", "ShiftD", "Any", or combinations like "ShiftA, ShiftB"
/// </summary>
public class ShiftSlotConverter : JsonConverter<ShiftSlot>
{
    public override ShiftSlot ReadJson(JsonReader reader, Type objectType, ShiftSlot existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return ShiftSlot.Any; // Default value

        // New format: string (enum name)
        if (reader.TokenType == JsonToken.String)
        {
            string stringValue = reader.Value?.ToString() ?? "Any";
            if (Enum.TryParse<ShiftSlot>(stringValue, out var result))
                return result;
            return ShiftSlot.Any; // Fallback
        }

        // Old format: integer - needs migration
        int rawValue = Convert.ToInt32(reader.Value);
        return rawValue switch
        {
            3 => ShiftSlot.ShiftC,  // Old ShiftC (3) -> New ShiftC (4)
            4 => ShiftSlot.ShiftD,  // Old ShiftD (4) -> New ShiftD (8)
            5 => ShiftSlot.Any,     // Old Any (5) -> New Any (128)
            _ => (ShiftSlot)rawValue // 0, 1, 2 unchanged; higher values are already new format
        };
    }

    public override void WriteJson(JsonWriter writer, ShiftSlot value, JsonSerializer serializer)
    {
        // Always write as string (enum name) to distinguish from old integer format
        writer.WriteValue(value.ToString());
    }
}

