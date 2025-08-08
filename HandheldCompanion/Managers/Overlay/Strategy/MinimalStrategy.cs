namespace HandheldCompanion.Managers.Overlay.Strategy;

public class MinimalStrategy: IOverlayStrategy
{
    public string GetConfig()
    {
        OverlayRow row1 = new();

        OverlayEntry fpsEntry = new("<APP>", "FF0000");
        WidgetFactory.CreateWidget("FPS", fpsEntry, WidgetLevel.FULL);
        row1.entries.Add(fpsEntry);

        return row1.ToString();
    }
}
