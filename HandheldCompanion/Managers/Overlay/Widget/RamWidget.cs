namespace HandheldCompanion.Managers.Overlay.Widget;

public class RamWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayRAMLevel)
        {
            case 2:
            case 1:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetMemoryUsage(), "GB");
                break;
        }
    }
}