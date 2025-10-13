using System.Management;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget : IWidget
{
    private float? TimeLeftInMinutes => PlatformManager.LibreHardware.GetBatteryTimeSpan();

    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayBATTLevel;
        if (_level == null)
        {
            return;
        }

        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryPower(), "W");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel(), "%");
                break;
        }

        if (IsBatteryCharging())
        {
            return;
        }

        OSDManager.AddElementIfNotNull(entry, TimeBatteryHours(), "h");
        OSDManager.AddElementIfNotNull(entry, TimeBatteryMinutes(), "min");
    }

    private static bool IsBatteryCharging()
    {
        using var searcher = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery");
        foreach (var o in searcher.Get())
        {
            var result = (ManagementObject)o;
            if (result["BatteryStatus"] is ushort status)
                return status is 6 or 7 or 8 or 9;
        }

        return false;
    }

    private int TimeBatteryHours()
    {
        if (TimeLeftInMinutes is not float minutes)
            return 0;

        return (int)(minutes / 60f);
    }

    private int TimeBatteryMinutes()
    {
        if (TimeLeftInMinutes is not float minutes)
            return 0;

        return (int)(minutes % 60f);
    }
}
