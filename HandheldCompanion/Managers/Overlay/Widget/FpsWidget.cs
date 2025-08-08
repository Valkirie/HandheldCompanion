namespace HandheldCompanion.Managers.Overlay.Widget;

public class FPSWidget: IWidget
{
    public void Build(OverlayEntry entry, short? level = null)
    {
        var _level = level ?? OSDManager.OverlayFPSLevel;
        switch (_level)
        {
            case WidgetLevel.MINIMAL:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                break;
            case WidgetLevel.FULL:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                entry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                break;
        }
    }
}