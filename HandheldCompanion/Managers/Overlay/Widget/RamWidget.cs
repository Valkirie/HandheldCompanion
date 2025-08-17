namespace HandheldCompanion.Managers.Overlay.Widget;

public class RamWidget : IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayRAMLevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetMemoryUsage(), PlatformManager.LibreHardware.GetMemoryTotal(), "GB");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetMemoryUsage(), "GB");
                break;
        }
    }
}