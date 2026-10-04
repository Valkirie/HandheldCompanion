namespace HandheldCompanion.Managers.Overlay.Strategy;

public class MinimalStrategy : IOverlayStrategy
{
    public string GetConfig()
    {
        OverlayRow row1 = new();

        OverlayEntry fpsEntry = new("<APP>", OverlayColors.Color("FPS"));
        WidgetFactory.CreateWidget("FPS", fpsEntry, WidgetLevel.MINIMAL);
        row1.entries.Add(fpsEntry);

        return row1.ToString();
    }
}
