using HandheldCompanion.GraphicsProcessingUnit;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class GpuWidget : IWidget
{
    private readonly GPU? _gpu = GPUManager.GetCurrent();

    public void Build(OverlayEntry entry, short? level = null)
    {
        if (_gpu == null)
        {
            return;
        }

        var _level = level ?? OSDManager.OverlayGPULevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                OSDManager.AddElementIfNotNull(entry, _gpu.HasLoad() ? _gpu.GetLoad() : PlatformManager.LibreHardware.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasPower() ? _gpu.GetPower() : PlatformManager.LibreHardware.GetGPUPower(), "W");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasTemperature() ? _gpu.GetTemperature() : PlatformManager.LibreHardware.GetGPUTemperature(), "C");
                break;
            case WidgetLevel.MINIMAL:
                OSDManager.AddElementIfNotNull(entry, _gpu.HasLoad() ? _gpu.GetLoad() : PlatformManager.LibreHardware.GetGPULoad(), "%");
                OSDManager.AddElementIfNotNull(entry, _gpu.HasPower() ? _gpu.GetPower() : PlatformManager.LibreHardware.GetGPUPower(), "W");
                break;
        }
    }
}