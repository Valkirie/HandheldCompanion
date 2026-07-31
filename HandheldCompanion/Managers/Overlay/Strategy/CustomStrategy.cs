using System.Collections.Generic;

namespace HandheldCompanion.Managers.Overlay.Strategy;

public class CustomStrategy : IOverlayStrategy
{
    public string? GetConfig()
    {
        bool horizontal = ManagerFactory.settingsManager.GetInt("OnScreenDisplayCustomOrientation") == 0;
        List<OverlayEntry> entries = [];
        for (int i = 0; i < OSDManager.OverlayCount; i++)
        {
            var name = OSDManager.OverlayOrder[i];
            var entry = CreateEntry(name, !horizontal);
            if (entry is not null)
                entries.Add(entry);
        }

        if (horizontal)
        {
            OverlayRow row = new();
            row.entries.AddRange(entries);
            return row.ToString();
        }

        List<string> rows = [];
        foreach (var entry in entries)
        {
            OverlayRow row = new();
            row.entries.Add(entry);
            rows.Add(row.ToString());
        }

        return string.Join("\n", rows);
    }


    private static OverlayEntry? CreateEntry(string name, bool indent)
    {
        OverlayEntry entry = new(name, OverlayColors.EntryColor(name), indent);
        WidgetFactory.CreateWidget(name, entry);

        // Skip empty rows
        if (entry.elements.Count == 0)
        {
            return null;
        }

        return entry;
    }
}