using HandheldCompanion.Managers.Overlay;
using HandheldCompanion.Managers.Overlay.Strategy;
using System;
using System.Collections.Generic;

namespace HandheldCompanion.Managers;

public class OverlayManager
{
    private static readonly Dictionary<int, IOverlayStrategy> _configs = new()
    {
        { 0, new DisabledStrategy() },
        { 1, new MinimalStrategy() },
        { 2, new ExtendedStrategy() },
        { 3, new FullStrategy() },
        { 4, new CustomStrategy() },
        { 5, new ExternalStrategy()}
    };

    public string? GetConfig(int level)
    {
        if (!_configs.TryGetValue(level, out var config))
            throw new InvalidOperationException("Unknown overlay level " + level);

        return config?.GetConfig();
    }
}