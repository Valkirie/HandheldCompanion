namespace HandheldCompanion.Devices;

/// <summary>
/// Pure helpers for OneXPlayer battery EC mapping, capture, and restore fallbacks.
/// </summary>
internal static class OneXPlayerBatteryEc
{
    public const int UnsavedValue = -1;
    public const byte FallbackLimitPercent = 100;
    public const byte FallbackBypassValue = 0x00;

    public static byte MapBypassModeIndex(int modeIndex) => modeIndex switch
    {
        1 => 0x01, // bypass on, charges during sleep/shutdown
        2 => 0x03, // bypass always on
        _ => 0x00, // bypass off
    };

    /// <summary>
    /// Capture only when both previous settings are still unsaved.
    /// A failed restore or a duplicate toggle-on must not overwrite real OEM values
    /// with HC's already-applied EC bytes.
    /// </summary>
    public static bool ShouldCapture(int previousLimitPercent, int previousBypassValue) =>
        previousLimitPercent == UnsavedValue && previousBypassValue == UnsavedValue;

    /// <summary>
    /// Restores captured EC values when both previous settings are present;
    /// otherwise falls back to limit 100 and bypass 0x00.
    /// </summary>
    public static (byte limitPercent, byte bypassValue) ResolveRestore(int previousLimitPercent, int previousBypassValue)
    {
        bool hasLimit = previousLimitPercent >= 0 && previousLimitPercent <= 100;
        bool hasBypass = previousBypassValue >= 0 && previousBypassValue <= byte.MaxValue;
        if (hasLimit && hasBypass)
            return ((byte)previousLimitPercent, (byte)previousBypassValue);

        return (FallbackLimitPercent, FallbackBypassValue);
    }
}
