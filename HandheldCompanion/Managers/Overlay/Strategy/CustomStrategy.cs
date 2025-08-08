using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.Overlay.Widget;

namespace HandheldCompanion.Managers.Overlay.Strategy;

public class CustomStrategy: IOverlayStrategy
{
    public string? GetConfig()
    {
        List<string> Content = [];
        for (int i = 0; i < OSDManager.OverlayCount; i++)
        {
            var name = OSDManager.OverlayOrder[i];
            var content = EntryContent(name);
            if (content == "") continue;
            Content.Add(content);
        }

        return string.Join("\n", Content);
    }


    private static string EntryContent(string name)
    {
        OverlayRow row = new();
        OverlayEntry entry = new(name, OverlayColors.EntryColor(name), true);
        WidgetFactory.CreateWidget(name, entry);

        // Skip empty rows
        if (entry.elements.Count == 0) return "";
        row.entries.Add(entry);
        return row.ToString();
    }
}