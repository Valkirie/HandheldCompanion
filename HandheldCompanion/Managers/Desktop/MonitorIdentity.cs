using HandheldCompanion.Properties;
using HandheldCompanion.Shared;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HandheldCompanion.Managers.Desktop;

/// <summary>
/// Reads monitor identity from the device path and the EDID, and matches connected screens to known monitors.
/// Matching never guesses: an exact path, then a unique EDID serial, then a unique EDID text serial,
/// then a unique model when serials do not contradict, otherwise a new monitor.
/// </summary>
public static class MonitorIdentity
{
    // \\?\DISPLAY#<manufacturer+product>#<instance>#{interface class guid}
    private static readonly Regex DevicePathRegex = new(@"^\\\\\?\\DISPLAY#(?<model>[^#]+)#(?<instance>[^#]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InterfaceGuidRegex = new(@"#\{[0-9a-f\-]+\}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // GDI device name of a display source, \\.\DISPLAYn
    private static readonly Regex DisplayNumberRegex = new(@"^\\\\\.\\DISPLAY(?<number>\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // serial numbers some vendors write to every unit
    private static readonly HashSet<uint> InvalidSerialNumbers = [0x00000000, 0x01010101, 0xFFFFFFFF];

    /// <summary>
    /// Strips the trailing interface class guid, which is the same for every monitor.
    /// </summary>
    public static string NormalizePath(string? devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
            return string.Empty;

        return InterfaceGuidRegex.Replace(devicePath.Trim(), string.Empty);
    }

    public static string GetModelKey(string manufacturer, ushort productCode)
    {
        if (string.IsNullOrEmpty(manufacturer))
            return string.Empty;

        return $"{manufacturer.ToUpperInvariant()}{productCode:X4}";
    }

    public static bool IsValidSerialNumber(uint serialNumber) => !InvalidSerialNumbers.Contains(serialNumber);

    /// <summary>
    /// A text serial is usable when it has a letter or digit and isn't one repeated character, e.g. "0000000000".
    /// </summary>
    public static bool IsValidSerialString(string? serialString)
    {
        string text = serialString?.Trim() ?? string.Empty;
        return text.Any(char.IsLetterOrDigit) && text.Distinct().Count() > 1;
    }

    /// <summary>
    /// Numeric EDID serial with the text serial, empty when the numeric serial is invalid.
    /// </summary>
    public static string GetSerialKey(string manufacturer, ushort productCode, uint serialNumber, string serialString)
    {
        string model = GetModelKey(manufacturer, productCode);
        if (string.IsNullOrEmpty(model) || !IsValidSerialNumber(serialNumber))
            return string.Empty;

        return $"{model}|{serialNumber:X8}|{serialString?.Trim() ?? string.Empty}";
    }

    /// <summary>
    /// EDID text serial alone, the fallback for monitors whose numeric serial is invalid.
    /// Empty when the numeric serial is valid or the text serial isn't usable.
    /// </summary>
    public static string GetSerialTextKey(string manufacturer, ushort productCode, uint serialNumber, string serialString)
    {
        string model = GetModelKey(manufacturer, productCode);
        if (string.IsNullOrEmpty(model) || IsValidSerialNumber(serialNumber) || !IsValidSerialString(serialString))
            return string.Empty;

        return $"{model}|{serialString.Trim()}";
    }

    /// <summary>
    /// Name shown for a connected monitor: EDID name, then the stored known monitor name,
    /// then manufacturer and product code, then "Display N" (1-based position).
    /// </summary>
    public static string GetDisplayName(DesktopScreen screen, KnownMonitor? known, int position)
    {
        return GetMonitorName(screen, known) ?? string.Format(Resources.DisplayMonitor_Fallback, position);
    }

    /// <summary>
    /// Name of a connected monitor prefixed with the number Windows shows for it in Settings > Display,
    /// e.g. "Display 1: VX1755". A monitor without a usable name is just "Display 1".
    /// </summary>
    public static string GetDisplayLabel(DesktopScreen screen, KnownMonitor? known, int position)
    {
        int number = GetDisplayNumber(screen);
        string? name = GetMonitorName(screen, known);

        if (number <= 0)
            return name ?? string.Format(Resources.DisplayMonitor_Fallback, position);

        return name is null ? string.Format(Resources.DisplayMonitor_Fallback, number) : FormatDisplayLabel(number, name);
    }

    /// <summary>
    /// "Display N: name", the way Settings > Display numbers its displays.
    /// </summary>
    public static string FormatDisplayLabel(int number, string name)
    {
        return string.Format(Resources.DisplayMonitor_Label, number, name);
    }

    /// <summary>
    /// Windows display number, read from the GDI device name of the screen (\\.\DISPLAYn), 0 when unknown.
    /// Settings > Display numbers its displays the same way.
    /// </summary>
    public static int GetDisplayNumber(DesktopScreen screen)
    {
        Match match = DisplayNumberRegex.Match(screen.screen?.DeviceName ?? string.Empty);
        if (match.Success && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            return number;

        return 0;
    }

    // EDID name, then the stored known monitor name, then manufacturer and product code
    private static string? GetMonitorName(DesktopScreen screen, KnownMonitor? known)
    {
        string? name = CleanName(screen.FriendlyName) ?? CleanName(known?.FriendlyName);
        if (name is not null)
            return name;

        string manufacturer = (screen.EdidManufacturer ?? string.Empty).Trim('\0', ' ');
        if (!string.IsNullOrEmpty(manufacturer) && screen.EdidProductCode != 0)
            return $"{manufacturer} {screen.EdidProductCode:X4}";

        return null;
    }

    // null for empty or generic names (\\.\DISPLAYx device names, "Generic PnP Monitor")
    private static string? CleanName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim('\0', ' ', '\t', '\r', '\n');
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.StartsWith(@"\\.\", StringComparison.Ordinal))
            return null;
        if (trimmed.StartsWith("Generic ", StringComparison.OrdinalIgnoreCase))
            return null;
        return trimmed;
    }

    /// <summary>
    /// Fills the EDID identity fields of a screen from its device path and the registry EDID.
    /// </summary>
    public static void Populate(DesktopScreen screen)
    {
        screen.NormalizedPath = NormalizePath(screen.DevicePath);

        Match match = DevicePathRegex.Match(screen.DevicePath ?? string.Empty);
        if (!match.Success)
            return;

        string model = match.Groups["model"].Value;
        string instance = match.Groups["instance"].Value;

        // the hardware id is built from the EDID manufacturer and product code, use it when the EDID can't be read
        if (model.Length == 7 && ushort.TryParse(model.Substring(3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort productCode))
        {
            screen.EdidManufacturer = model.Substring(0, 3).ToUpperInvariant();
            screen.EdidProductCode = productCode;
        }

        try
        {
            using RegistryKey? deviceParams = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{model}\{instance}\Device Parameters");
            if (deviceParams?.GetValue("EDID") is byte[] edid && edid.Length >= 128)
                ParseEdid(screen, edid);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("Failed to read EDID for {0}: {1}", screen.DevicePath ?? string.Empty, ex.Message);
        }
    }

    private static void ParseEdid(DesktopScreen screen, byte[] edid)
    {
        // bytes 8-9: manufacturer id, three 5-bit letters (big endian)
        int manufacturer = (edid[8] << 8) | edid[9];
        char c1 = (char)('A' - 1 + ((manufacturer >> 10) & 0x1F));
        char c2 = (char)('A' - 1 + ((manufacturer >> 5) & 0x1F));
        char c3 = (char)('A' - 1 + (manufacturer & 0x1F));
        if (char.IsLetter(c1) && char.IsLetter(c2) && char.IsLetter(c3))
            screen.EdidManufacturer = new string([c1, c2, c3]);

        // bytes 10-11: product code, bytes 12-15: serial number (little endian)
        screen.EdidProductCode = (ushort)(edid[10] | (edid[11] << 8));
        screen.EdidSerialNumber = BitConverter.ToUInt32(edid, 12);

        // display descriptors: 0xFF holds the serial number as text
        const int dtdStart = 54;
        for (int i = 0; i < 4; i++)
        {
            int off = dtdStart + i * 18;
            if (edid[off] != 0x00 || edid[off + 1] != 0x00 || edid[off + 3] != 0xFF)
                continue;

            string raw = Encoding.ASCII.GetString(edid, off + 5, 13);
            int end = raw.IndexOf('\n');
            if (end >= 0)
                raw = raw.Substring(0, end);

            screen.EdidSerialString = raw.Replace("\0", string.Empty).Trim();
            break;
        }
    }

    /// <summary>
    /// Assigns a known monitor to every connected screen, adding new known monitors when needed.
    /// 1. exact device path (interface guid stripped)
    /// 2. EDID serial, when valid and unique among connected and known monitors
    /// 3. EDID text serial, when the numeric serial is invalid and the text is usable and unique
    /// 4. manufacturer + product, when exactly one known and one connected monitor have that model
    ///    and their serials do not identify different units
    /// 5. otherwise a new known monitor
    /// The stored path is updated on a serial, text serial or model match.
    /// </summary>
    public static void Assign(IReadOnlyList<DesktopScreen> screens, List<KnownMonitor> known)
    {
        DateTime now = DateTime.Now;
        HashSet<Guid> claimed = [];
        List<DesktopScreen> pending = [];

        // 1. exact device path
        foreach (DesktopScreen screen in screens)
        {
            KnownMonitor? monitor = string.IsNullOrEmpty(screen.NormalizedPath)
                ? null
                : known.FirstOrDefault(k => !claimed.Contains(k.Id) && string.Equals(k.DevicePath, screen.NormalizedPath, StringComparison.OrdinalIgnoreCase));

            if (monitor is null)
                pending.Add(screen);
            else
                Bind(screen, monitor, claimed, now, false);
        }

        // 2. EDID serial
        MatchUniqueKey(screens, known, pending, claimed, now, s => s.SerialKey, k => k.SerialKey);

        // 3. EDID text serial
        MatchUniqueKey(screens, known, pending, claimed, now, s => s.SerialTextKey, k => k.SerialTextKey);

        // 4. manufacturer + product, skipped when serials identify different units
        MatchUniqueKey(screens, known, pending, claimed, now, s => s.ModelKey, k => k.ModelKey, SerialsContradict);

        // 5. new monitor
        foreach (DesktopScreen screen in pending)
        {
            KnownMonitor monitor = new()
            {
                DevicePath = screen.NormalizedPath,
                FirstSeen = now,
            };
            known.Add(monitor);

            LogManager.LogInformation("New monitor {0} ({1}) added to known monitors", screen.FriendlyName, screen.ModelKey);
            Bind(screen, monitor, claimed, now, false);
        }
    }

    // binds pending screens whose key is unique among connected screens and matches exactly one unclaimed known monitor
    private static void MatchUniqueKey(IReadOnlyList<DesktopScreen> screens, List<KnownMonitor> known, List<DesktopScreen> pending,
        HashSet<Guid> claimed, DateTime now, Func<DesktopScreen, string> screenKey, Func<KnownMonitor, string> knownKey,
        Func<DesktopScreen, KnownMonitor, bool>? reject = null)
    {
        foreach (DesktopScreen screen in pending.ToList())
        {
            string key = screenKey(screen);
            if (string.IsNullOrEmpty(key) || screens.Count(s => screenKey(s) == key) != 1)
                continue;

            List<KnownMonitor> matches = known.Where(k => knownKey(k) == key).ToList();
            if (matches.Count != 1 || claimed.Contains(matches[0].Id))
                continue;
            if (reject?.Invoke(screen, matches[0]) == true)
                continue;

            Bind(screen, matches[0], claimed, now, true);
            pending.Remove(screen);
        }
    }

    // a replacement of the same model must not inherit the previous unit's ID
    private static bool SerialsContradict(DesktopScreen screen, KnownMonitor known)
    {
        if (IsValidSerialNumber(screen.EdidSerialNumber) && IsValidSerialNumber(known.SerialNumber)
            && screen.EdidSerialNumber != known.SerialNumber)
            return true;

        if (IsValidSerialString(screen.EdidSerialString) && IsValidSerialString(known.SerialString)
            && !string.Equals(screen.EdidSerialString.Trim(), known.SerialString.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void Bind(DesktopScreen screen, KnownMonitor monitor, HashSet<Guid> claimed, DateTime now, bool rebindPath)
    {
        claimed.Add(monitor.Id);
        screen.KnownMonitorId = monitor.Id;

        if (rebindPath && !string.IsNullOrEmpty(screen.NormalizedPath)
            && !string.Equals(monitor.DevicePath, screen.NormalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            LogManager.LogInformation("Monitor {0} moved from {1} to {2}", screen.FriendlyName, monitor.DevicePath, screen.NormalizedPath);
            monitor.DevicePath = screen.NormalizedPath;
        }

        // an exact-path match can succeed without EDID; keep the stored identity then
        if (IsValidSerialNumber(screen.EdidSerialNumber) || IsValidSerialString(screen.EdidSerialString))
        {
            monitor.Manufacturer = screen.EdidManufacturer;
            monitor.ProductCode = screen.EdidProductCode;
            monitor.SerialNumber = screen.EdidSerialNumber;
            monitor.SerialString = screen.EdidSerialString;
        }
        // keep the stored name when the current one is empty or generic
        if (CleanName(screen.FriendlyName) is not null)
            monitor.FriendlyName = screen.FriendlyName;
        monitor.IsInternal = screen.IsInternal;
        monitor.LastSeen = now;
    }
}
