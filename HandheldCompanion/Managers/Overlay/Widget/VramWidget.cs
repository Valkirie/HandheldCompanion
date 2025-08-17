namespace HandheldCompanion.Managers.Overlay.Widget;

public class VramWidget : IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayVRAMLevel;
        switch (_level)
        {
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPUMemory(), "GB");
                break;
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPUMemory(), PlatformManager.LibreHardware.GetGPUMemoryTotal(), "GB");
                break;
        }
    }
}