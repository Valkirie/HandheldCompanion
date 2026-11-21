using Newtonsoft.Json;
using System;

namespace HandheldCompanion.Managers.Desktop;

/// <summary>
/// A monitor HC has seen at least once. Entries are kept when the monitor is not connected,
/// so per-monitor profile settings survive unplugging. Profiles reference monitors by Id.
/// </summary>
[Serializable]
public class KnownMonitor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // device path without the trailing interface class guid, re-bound when the monitor is matched by EDID
    public string DevicePath { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;
    public ushort ProductCode { get; set; }
    public uint SerialNumber { get; set; }
    public string SerialString { get; set; } = string.Empty;

    public string FriendlyName { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }

    [JsonIgnore]
    public string ModelKey => MonitorIdentity.GetModelKey(Manufacturer, ProductCode);

    [JsonIgnore]
    public string SerialKey => MonitorIdentity.GetSerialKey(Manufacturer, ProductCode, SerialNumber, SerialString);

    [JsonIgnore]
    public string SerialTextKey => MonitorIdentity.GetSerialTextKey(Manufacturer, ProductCode, SerialNumber, SerialString);
}
