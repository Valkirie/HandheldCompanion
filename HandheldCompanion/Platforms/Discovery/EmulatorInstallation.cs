using System.Collections.Generic;
using System;

namespace HandheldCompanion.Platforms.Discovery;

public sealed class EmulatorInstallation
{
    public required EmulatorDefinition Definition { get; init; }
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> ExecutablePaths { get; init; } = [];
    public IReadOnlyList<string> ConfigFiles { get; init; } = [];
    public IReadOnlyList<string> ContentPaths { get; init; } = [];
}
