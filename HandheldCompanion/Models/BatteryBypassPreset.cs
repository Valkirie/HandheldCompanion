using HandheldCompanion.Properties;

namespace HandheldCompanion.Models;

public class BatteryBypassPreset(string keyName)
{
    public string NameKey { get; set; } = keyName;

    public string DisplayName
    {
        get => Resources.ResourceManager.GetString(NameKey);
    }
}