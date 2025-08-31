namespace HandheldCompanion.Managers.Overlay;

public struct OverlayColors
{
    public const string DEFAULT_COLOR = "FFFFFF";
    public const string GPU_COLOR = "8040";
    public const string VRAM_COLOR = "8000FF";
    public const string CPU_COLOR = "80FF";
    public const string RAM_COLOR = "FF80C0";
    public const string BATT_COLOR = "FF8000";
    public const string FPS_COLOR = "FF0000";

    public static string EntryColor(string name)
    {
        return name.ToUpper() switch
        {
            "FPS" => FPS_COLOR,
            "CPU" => CPU_COLOR,
            "GPU" => GPU_COLOR,
            "RAM" => RAM_COLOR,
            "VRAM" => VRAM_COLOR,
            "BATT" => BATT_COLOR,
            _ => DEFAULT_COLOR
        };
    }
}