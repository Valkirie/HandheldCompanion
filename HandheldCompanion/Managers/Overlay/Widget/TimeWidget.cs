using System;
using System.Globalization;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class TimeWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayTimeLevel)
        {
            case 2:
                entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString(CultureInfo.InvariantCulture), ""));
                break;
            case 1:
                entry.elements.Add(new OverlayEntryElement(DateTime.Now.ToString("t"), ""));
                break;
        }
    }
}