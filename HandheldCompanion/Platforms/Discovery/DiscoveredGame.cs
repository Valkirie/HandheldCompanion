using GameLib.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Platforms.Discovery;

public sealed class DiscoveredGame : IGame
{
    public DiscoveredGame(string name, string romPath, string emulatorPath, string arguments, GamePlatform platformType, bool isEmulator = false, IEnumerable<string>? executables = null)
    {
        Name = name;
        RomPath = Path.GetFullPath(romPath);
        Executable = emulatorPath;
        Executables = executables?.ToArray() ?? [emulatorPath];
        Arguments = arguments;
        PlatformType = platformType;
        IsEmulator = isEmulator;
        WorkingDir = Path.GetDirectoryName(emulatorPath) ?? string.Empty;
        LaunchString = emulatorPath;
    }

    public string Id => RomPath;
    public Guid LauncherId => Guid.Empty;
    public string Name { get; }
    public string InstallDir => Path.GetDirectoryName(RomPath) ?? string.Empty;
    public string RomPath { get; }
    public string Executable { get; }
    public Icon? ExecutableIcon => null;
    public IEnumerable<string> Executables { get; }
    public string WorkingDir { get; }
    public string LaunchString { get; }
    public DateTime InstallDate => File.GetCreationTime(RomPath);
    public bool IsRunning => false;
    public string Arguments { get; }
    public GamePlatform PlatformType { get; }
    public bool IsEmulator { get; }
}
