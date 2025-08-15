namespace HandheldCompanion.Managers.Overlay.Widget;

public class VramWidget: IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayVRAMLevel;
        switch (_level)
        {
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), "GB");
                break;
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), PlatformManager.LibreHardwareMonitor.GetGPUMemoryTotal(), "GB");
                break;
        }
    }
}