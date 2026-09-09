using System;

namespace HandheldCompanion.Managers.Overlay;

public struct OverlayColors
{
    public const string DEFAULT_COLOR = "FFFFFF";
    public const string GPU_COLOR = "008040";
    public const string VRAM_COLOR = "8000FF";
    public const string CPU_COLOR = "0080FF";
    public const string RAM_COLOR = "FF80C0";
    public const string BATT_COLOR = "FF8000";
    public const string FPS_COLOR = "FF0000";

    private static double _brightness = 0.5;

    public static double Brightness
    {
        get => _brightness;
        set => _brightness = Math.Clamp(value, 0, 1);
    }

    public static string Color(string name)
    {
        string color = name.ToUpperInvariant() switch
        {
            "FPS" => FPS_COLOR,
            "CPU" => CPU_COLOR,
            "GPU" => GPU_COLOR,
            "RAM" => RAM_COLOR,
            "VRAM" => VRAM_COLOR,
            "BATT" => BATT_COLOR,
            _ => DEFAULT_COLOR
        };

        return ScaleColor(color);
    }

    public static string ScaleColor(string color)
    {
        int red = Convert.ToInt32(color[..2], 16);
        int green = Convert.ToInt32(color.Substring(2, 2), 16);
        int blue = Convert.ToInt32(color.Substring(4, 2), 16);

        return $"{Scale(red):X2}{Scale(green):X2}{Scale(blue):X2}";
    }

    private static int Scale(int channel) => (int)Math.Round(channel * Brightness);

    public static string EntryColor(string name)
    {
        return Color(name);
    }
}