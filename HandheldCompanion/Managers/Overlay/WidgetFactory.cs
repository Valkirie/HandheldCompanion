using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.Overlay.Widget;

namespace HandheldCompanion.Managers.Overlay;

public class WidgetFactory
{
    private static Dictionary<string, IWidget> Widgets =>
        new()
        {
            {"TIME", new TimeWidget()},
            {"BATT", new BatteryWidget()},
            {"VRAM", new VramWidget()},
            {"CPU", new CpuWidget()},
            {"RAM", new RamWidget()},
            {"FPS", new FPSWidget()},
            {"GPU", new GpuWidget()}
        };

    public static void CreateWidget(string name, OverlayEntry entry, short? level = null)
    {
        if (!Widgets.TryGetValue(name.ToUpper(), out var widget))
        {
            return;
        }

        widget.Build(entry, level);
    }
}