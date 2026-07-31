namespace HandheldCompanion.Managers.Overlay.Widget;

public class GpuWidget : IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayGPULevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPUPower(), "W");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPUTemperature(), "C");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetGPUPower(), "W");
                break;
        }
    }
}