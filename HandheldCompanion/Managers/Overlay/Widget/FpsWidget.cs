using RTSSSharedMemoryNET;

namespace HandheldCompanion.Managers.Overlay.Widget;

public class FPSWidget : IWidget
{
    private const int FrametimeHistorySize = 64;
    private const int GraphWidth = 64;
    private const int GraphHeight = 8;
    private const int GraphMargin = 1;
    private const float GraphMinMs = 0f;
    private const float GraphMaxMs = 50f;
    private const string GraphColor = "00FFFF";

    public void Build(OverlayEntry entry, short? level = null)
    {
        float minFt = 0.0f, avgFt = 0.0f, maxFt = 0.0f;
        uint graphOffset = 0;
        var flags = (EMBEDDED_OBJECT_GRAPH)0;

        uint processId = (uint)(OSDManager.OnScreenAppEntry?.ProcessId ?? 0);

        uint graphSize = OSDManager.OnScreenDisplay?.EmbedGraphDirect(
            graphOffset,
            processId,
            (uint)FrametimeHistorySize,
            GraphWidth,
            GraphHeight,
            GraphMargin,
            GraphMinMs,
            GraphMaxMs,
            flags,
            out minFt,
            out avgFt,
            out maxFt) ?? 0;

        string yAxisMax = $"<S=50><C=808080>{GraphMaxMs:F0}ms<C><S>";
        string yAxisMin = $"<S=50><C=808080>{GraphMinMs:F0}ms<C><S>";

        var _level = level ?? OSDManager.OverlayFPSLevel;
        switch (_level)
        {
            case WidgetLevel.MINIMAL:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                break;
            case WidgetLevel.FULL:
                entry.elements.Add(new OverlayEntryElement("<FR>", "FPS"));
                entry.elements.Add(new OverlayEntryElement("<FT>", "ms"));
                entry.elements.Add(new OverlayEntryElement($"<C={GraphColor}><OBJ={graphOffset:X8}><C>"));

                /*
                entry.elements.Add(new OverlayEntryElement($"<S=50><C=808080>min:<C=00FF00>{minFt:F1}", "ms"));
                entry.elements.Add(new OverlayEntryElement($"<S=50><C=808080>avg:<C=FFFF00>{avgFt:F1}", "ms"));
                entry.elements.Add(new OverlayEntryElement($"<S=50><C=808080>max:<C=FF6600>{maxFt:F1}", "ms"));
                */
                break;
        }
    }

    /*
    private static void AppendFrameGraph(OverlayEntry entry)
    {
        if (graphSize == 0)
            return;

        string statsLabel = "";
        if (minFt > 0 && maxFt > 0)
        {
            statsLabel =
                $"<S=50><C=808080>min:<C=00FF00>{minFt:F1}ms " +
                $"<C=808080>avg:<C=FFFF00>{avgFt:F1}ms " +
                $"<C=808080>max:<C=FF6600>{maxFt:F1}ms<C><S>";
        }

        string content =
            $"{yAxisMax}" +
            $"<C={graphColor}><OBJ={graphOffset:X8}><C>\n" +
            $"{yAxisMin}\n" +
            $"{statsLabel}";

        // RAW element (no wrapping)
        entry.elements.Add(new OverlayEntryElement(content, ""));
    }
    */
}