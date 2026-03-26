using HandheldCompanion.Managers.Overlay.Widget;
using System.Collections.Generic;

namespace HandheldCompanion.Managers.Overlay;

public class WidgetFactory
{
    private static readonly Dictionary<string, IWidget> Widgets = new()
    {
        {"TIME", new TimeWidget()},
        {"BATT", new BatteryWidget()},
        {"VRAM", new VramWidget()},
        {"CPU", new CpuWidget()},
        {"RAM", new RamWidget()},
        {"FPS", new FPSWidget()},
        {"GPU", new GpuWidget()}
    };

    public static void CreateWidget(string key, OverlayEntry entry, short? level = null)
    {
        if (!Widgets.TryGetValue(key, out IWidget? widget))
            return;

        widget.Build(entry, level);
    }
}