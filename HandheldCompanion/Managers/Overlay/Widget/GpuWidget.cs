using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class GpuWidget: IWidget
{
    private readonly GPU? _gpu = GPUManager.GetCurrent();
    
    public void Build(OverlayEntry entry)
    {
        if (_gpu == null)
        {
            return;
        }
        
        switch (OSDManager.OverlayGPULevel)
        {
            case 2:
                OSDManager.AddElementIfNotNull(entry, _gpu.HasLoad() ? _gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasPower() ? _gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasTemperature() ? _gpu.GetTemperature() : PlatformManager.LibreHardwareMonitor.GetGPUTemperature(), "C");
                break;
            case 1:
                OSDManager.AddElementIfNotNull(entry, _gpu.HasLoad() ? _gpu.GetLoad() : PlatformManager.LibreHardwareMonitor.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasPower() ? _gpu.GetPower() : PlatformManager.LibreHardwareMonitor.GetGPUPower(), "W");
                break;
        }
    }
}