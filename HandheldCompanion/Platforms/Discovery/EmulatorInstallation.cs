using System.Collections.Generic;

namespace HandheldCompanion.Platforms.Discovery;

public sealed class EmulatorInstallation
{
    public required EmulatorDefinition Definition { get; init; }
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> ExecutablePaths { get; init; } = [];
    public IEnumerable<string> ConfigFiles { get; init; } = [];
    public IEnumerable<string> ContentPaths { get; init; } = [];
}
