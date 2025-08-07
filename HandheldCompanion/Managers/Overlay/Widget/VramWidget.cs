namespace HandheldCompanion.Managers.Overlay.Widget;

public class VramWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayVRAMLevel)
        {
            case 1:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), "GB");
                break;
            case 2:
                OSDManager.AddElementIfNotNull(entry, PlatformManager.LibreHardwareMonitor.GetGPUMemory(), PlatformManager.LibreHardwareMonitor.GetGPUMemoryTotal(), "GB");
                break;
        }
    }
}