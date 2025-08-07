namespace HandheldCompanion.Managers.Overlay.Widget;

public class CpuWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayCPULevel)
        {
            case 2:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUTemperature(), "C");
                break;
            case 1:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetCPUPower(), "W");
                break;
        }
    }
}