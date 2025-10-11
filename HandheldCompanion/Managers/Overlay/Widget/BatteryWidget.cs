using System.Management;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class BatteryWidget: IWidget
{
    private float? TimeLeftInMinutes => PlatformManager.LibreHardwareMonitor.GetBatteryTimeSpan();

    public void Build(OverlayEntry entry, short? level = null)
    {
        var overlayBattLevel = level ?? OSDManager.OverlayBATTLevel;
        switch (overlayBattLevel)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryPower(), "W");
                if (!IsBatteryCharging() && null != TimeLeftInMinutes)
                {
                    OSDManager.AddElementIfNotNull(entry, TimeBatteryHours(), "h");
                    OSDManager.AddElementIfNotNull(entry, TimeBatteryMinutes(), "m");
                }

                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetBatteryLevel(), "%");
                if (!IsBatteryCharging() && null != TimeLeftInMinutes)
                {
                    OSDManager.AddElementIfNotNull(entry, TimeBatteryHours(), "h");
                    OSDManager.AddElementIfNotNull(entry, TimeBatteryMinutes(), "m");
                }
                break;
        }
    }

    private static bool IsBatteryCharging()
    {
        var wmi = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
        var resultCollection = wmi.Get();
        foreach (var resultRow in resultCollection)
        {
            foreach (var property in resultRow.Properties)
            {
                if (property.Name != "BatteryStatus") continue;

                var value = property.Value.ToString();
                if (null == value)
                {
                    return false;
                }

                var status= ushort.Parse(value);

                return (status & 2) == 2 || (status & 3) == 3;
            }
        }

        return false;
    }

    private int TimeBatteryHours()
    {
        if (TimeLeftInMinutes != null)
        {
            return (int) (TimeLeftInMinutes / 60);
        }

        return 0;
    }

    private int TimeBatteryMinutes()
    {
        if (TimeLeftInMinutes != null)
        {
            return (int) (TimeLeftInMinutes - TimeLeftInMinutes / 60 * 60);
        }

        return 0;
    }
}