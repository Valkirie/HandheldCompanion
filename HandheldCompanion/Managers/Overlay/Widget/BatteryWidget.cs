namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayBATTLevel)
        {
            case 2:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                break;
            case 1:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan(), "min");
                break;
        }
    }
}