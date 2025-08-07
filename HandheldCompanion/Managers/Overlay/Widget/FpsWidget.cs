namespace HandheldCompanion.Managers.Overlay.Widget;

public class FPSWidget: IWidget
{
    public void Build(OverlayEntry entry)
    {
        switch (OSDManager.OverlayFPSLevel)
        {
            case 2:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                entry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                break;
            case 1:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                break;
        }
    }
}