using System;
using System.Collections.Generic;
using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers.OSDStrategy.Overlay;
using Sentry.Protocol;

namespace HandheldCompanion.Managers.OSDStrategy;

public class OverlayManager
{
    private readonly GPU? _gpu = GPUManager.GetCurrent();

    private readonly Dictionary<int, IOverlayStrategy> _configs;

    public OverlayManager()
    {
        _configs = new Dictionary<int, IOverlayStrategy>();
        if (_gpu is null)
        {
            return;
        }

        _configs.Add(0, new DisabledStrategy());
        _configs.Add(1, new MinimalStrategy(_gpu));
        _configs.Add(2, new ExtendedStrategy(_gpu));
        _configs.Add(3, new FullStrategy(_gpu));
        _configs.Add(4, new CustomStrategy(_gpu));
    }

    public string? GetConfig(int level)
    {
        if (_gpu is null || _configs.Count == 0)
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