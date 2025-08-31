namespace HandheldCompanion.Managers.Overlay.Strategy;


public class FullStrategy : IOverlayStrategy
{
    public string? GetConfig()
    {
        OverlayRow row1 = new(); // GPU
        OverlayRow row2 = new(); // CPU  
        OverlayRow row3 = new(); // RAM
        OverlayRow row4 = new(); // VRAM
        OverlayRow row5 = new(); // Battery
        OverlayRow row6 = new(); // FPS

        OverlayEntry GPUentry = new("GPU", OverlayColors.GPU_COLOR, true);
        WidgetFactory.CreateWidget("GPU", GPUentry, WidgetLevel.FULL);
        row1.entries.Add(GPUentry);

        OverlayEntry VRAMentry = new("VRAM", OverlayColors.VRAM_COLOR, true);
        WidgetFactory.CreateWidget("VRAM", VRAMentry, WidgetLevel.FULL);
        row4.entries.Add(VRAMentry);

        OverlayEntry CPUentry = new("CPU", OverlayColors.CPU_COLOR, true);
        WidgetFactory.CreateWidget("CPU", CPUentry, WidgetLevel.FULL);
        row2.entries.Add(CPUentry);

        OverlayEntry RAMentry = new("RAM", OverlayColors.RAM_COLOR, true);
        WidgetFactory.CreateWidget("RAM", RAMentry, WidgetLevel.FULL);
        row3.entries.Add(RAMentry);

        OverlayEntry BATTentry = new("BATT", OverlayColors.BATT_COLOR, true);
        WidgetFactory.CreateWidget("BATT", BATTentry, WidgetLevel.FULL);
        row5.entries.Add(BATTentry);

        OverlayEntry fpsEntry = new("<APP>", OverlayColors.FPS_COLOR, true);
        WidgetFactory.CreateWidget("FPS", fpsEntry, WidgetLevel.FULL);
        row6.entries.Add(fpsEntry);

        return string.Join("\n",
            row1.ToString(),
            row2.ToString(),
            row3.ToString(),
            row4.ToString(),
            row5.ToString(),
            row6.ToString()
        );
    }
}