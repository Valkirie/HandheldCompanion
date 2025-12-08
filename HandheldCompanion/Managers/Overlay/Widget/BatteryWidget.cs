using System;
using System.Windows.Forms;
using Windows.Devices.Power;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget : IWidget
{
    private float? TimeLeftInMinutes => PlatformManager.LibreHardware.GetBatteryTimeSpan() ?? BatteryLifeRemainingInMinutes();

    private float? BatteryLifeRemainingInMinutes()
    {
        int secs = SystemInformation.PowerStatus.BatteryLifeRemaining; // -1 if unknown
        if (secs >= 0)
        {
            TimeSpan t = TimeSpan.FromSeconds(secs);
            return (float)t.TotalMinutes;
        }

        return null;
    }

    private float? BatteryChargeRateInWatts()
    {
        BatteryReport report = Battery.AggregateBattery.GetReport();
        if (report.ChargeRateInMilliwatts.HasValue)
            return report.ChargeRateInMilliwatts / 1000.0f;

        return null;
    }

    private float BatteryLifePercent()
    {
        return SystemInformation.PowerStatus.BatteryLifePercent * 100.0f;
    }

    public void Build(OverlayEntry entry, short? level = null)
    {
        short _level = level ?? OSDManager.OverlayBATTLevel;

        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel() ?? BatteryLifePercent(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryPower() ?? BatteryChargeRateInWatts(), "W");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardware.GetBatteryLevel() ?? BatteryLifePercent(), "%");
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
