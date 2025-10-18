using System.Management;
using System.Windows.Forms;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget : IWidget
{
    private float? TimeLeftInMinutes => PlatformManager.LibreHardware.GetBatteryTimeSpan();

    public void Build(OverlayEntry entry, short? level = null)
    {
        short _level = level ?? OSDManager.OverlayBATTLevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryPower(), "W");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel(), "%");
                break;
            default:
                return;
        }

        if (IsBatteryCharging())
            return;

        if (!TimeLeftInMinutes.HasValue)
            return;

        OSDManager.AddElementIfNotNull(entry, TimeBatteryHours(), "h");
        OSDManager.AddElementIfNotNull(entry, TimeBatteryMinutes(), "min");
    }

    private static bool IsBatteryCharging() => SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Online;

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
