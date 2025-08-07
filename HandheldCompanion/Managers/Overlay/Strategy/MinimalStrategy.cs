namespace HandheldCompanion.Managers.Overlay.Strategy;

public class MinimalStrategy: IOverlayStrategy
{
    public string GetConfig()
    {
        OverlayRow row1 = new();

        OverlayEntry FPSentry = new("<APP>", "FF0000");
        FPSentry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
        FPSentry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
        row1.entries.Add(FPSentry);

        return row1.ToString();
    }
}
