using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Misc
{
    /// <summary>
    /// How a game profile defines suspend-on-sleep.
    /// Inherit defers to Device settings (the global SuspendOnSleep toggle).
    /// On and Off override that value for this game.
    /// </summary>
    public enum SuspendOnSleepMode
    {
        Inherit = 0,
        On = 1,
        Off = 2,
    }

    /// <summary>
    /// Legacy profile JSON stored a bool (false = not set, true = enable).
    /// </summary>
    public sealed class SuspendOnSleepModeConverter : JsonConverter<SuspendOnSleepMode>
    {
        public override SuspendOnSleepMode ReadJson(JsonReader reader, Type objectType, SuspendOnSleepMode existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Boolean:
                    return (bool)reader.Value! ? SuspendOnSleepMode.On : SuspendOnSleepMode.Inherit;
                case JsonToken.Integer:
                    return Convert.ToInt32(reader.Value) switch
                    {
                        1 => SuspendOnSleepMode.On,
                        2 => SuspendOnSleepMode.Off,
                        _ => SuspendOnSleepMode.Inherit,
                    };
                case JsonToken.String:
                    return Enum.TryParse((string?)reader.Value, true, out SuspendOnSleepMode mode)
                        ? mode
                        : SuspendOnSleepMode.Inherit;
                default:
                    return SuspendOnSleepMode.Inherit;
            }
        }

        public override void WriteJson(JsonWriter writer, SuspendOnSleepMode value, JsonSerializer serializer)
        {
            writer.WriteValue((int)value);
        }
    }
}
