using System;
using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.Overlay;

namespace HandheldCompanion.Managers;

public class OverlayManager
{
    private readonly GPU? _gpu = GPUManager.GetCurrent();

    private readonly Dictionary<int, IOverlayStrategy> _configs;

    public OverlayManager()
    {
        _configs = new Dictionary<int, IOverlayStrategy>
        {
            { 0, new DisabledStrategy() },
            { 1, new MinimalStrategy() },
            { 2, new ExtendedStrategy(_gpu) },
            { 3, new FullStrategy(_gpu) },
            { 4, new CustomStrategy(_gpu) }
        };
    }

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