using System;
using HandheldCompanion.Properties;

namespace HandheldCompanion.Models;

public class LEDPreset(string keyName, string icon, int value)
{
    public string NameKey { get; set; } = keyName;
    public string Icon { get; set; } = icon;
    public int Value { get; set; } = value;

    public string DisplayName
    {
        get => Resources.ResourceManager.GetString(NameKey);
    }

    public Uri IconPath
    {
        get => new Uri($"pack://application:,,,/Resources/led_presets/{Icon}");
    }
}