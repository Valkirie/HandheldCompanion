using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.Overlay.Widget;

namespace HandheldCompanion.Managers.Overlay;

public class WidgetFactory
{
    public static void CreateWidget(string name, OverlayEntry entry)
    {
        var widgets = new Dictionary<string, IWidget>
        {
            {"TIME", new TimeWidget()},
            {"BATT", new BatteryWidget()},
            {"VRAM", new VramWidget()},
            {"CPU", new CpuWidget()},
            {"RAM", new RamWidget()},
            {"FPS", new FPSWidget()},
            {"GPU", new GpuWidget()}
        };

        if (!widgets.TryGetValue(name.ToUpper(), out var widget))
        {
            return;
        }

        widget.Build(entry);
    }
}