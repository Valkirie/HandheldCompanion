namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget: IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayBATTLevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                break;
        }
    }
}