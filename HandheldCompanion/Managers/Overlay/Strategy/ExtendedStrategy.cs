namespace HandheldCompanion.Managers.Overlay.Strategy;

public class ExtendedStrategy : IOverlayStrategy
{
    public string GetConfig()
    {
        OverlayRow row1 = new();
        OverlayEntry FPSentry = new("<APP>", OverlayColors.Color("FPS"));
        WidgetFactory.CreateWidget("FPS", FPSentry, WidgetLevel.FULL);
        row1.entries.Add(FPSentry);

        OverlayEntry GPUentry = new("GPU", OverlayColors.Color("GPU"));
        WidgetFactory.CreateWidget("GPU", GPUentry, WidgetLevel.MINIMAL);
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", OverlayColors.Color("VRAM"));
        WidgetFactory.CreateWidget("VRAM", VRAMentry, WidgetLevel.MINIMAL);
        row1.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", OverlayColors.Color("CPU"));
        WidgetFactory.CreateWidget("CPU", CPUentry, WidgetLevel.MINIMAL);
        row1.entries.Add(CPUentry);

        OverlayEntry RAMentry = new("RAM", OverlayColors.Color("RAM"));
        WidgetFactory.CreateWidget("RAM", RAMentry, WidgetLevel.MINIMAL);
        row1.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", OverlayColors.Color("BATT"));
        WidgetFactory.CreateWidget("BATT", BATTentry, WidgetLevel.MINIMAL);
        row1.entries.Add(BATTentry);

        return row1.ToString();
    }
}
