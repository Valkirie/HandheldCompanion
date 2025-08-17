using System;
using System.Globalization;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class TimeWidget : IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayTimeLevel;
        switch (_level)
        {
            case WidgetLevel.FULL:
                entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString(CultureInfo.InvariantCulture), ""));
                break;
            case WidgetLevel.MINIMAL:
                entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString("t"), ""));
                break;
        }
    }
}