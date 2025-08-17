using System;
using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.Overlay;
using HandheldCompanion.Managers.Overlay.Strategy;

namespace HandheldCompanion.Managers;

public class OverlayManager
{
    private readonly GPU? _gpu = GPUManager.GetCurrent();

    private readonly Dictionary<int, IOverlayStrategy> _configs = new()
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
        if (_configs.Count == 0)
        {
            return null;
        }

        if (!_configs.ContainsKey(level))
        {
            throw new InvalidOperationException("Unknown overlay level " + level);
        }

        _configs.TryGetValue(level, out var config);

        return config?.GetConfig();
    }
}