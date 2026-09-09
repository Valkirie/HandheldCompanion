namespace HandheldCompanion.Platforms.Discovery;

public sealed class EmulatorDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required GamePlatform PlatformType { get; init; }
    public string PlatformColor { get; init; } = "#666666";
    public string PlatformGlyph { get; init; } = "\uF712";
    public string PlatformFont { get; init; } = "Simple Icons Fit";
    public double PlatformFontSize { get; init; } = 22;

    public string[] Executables { get; init; } = [];
    public string[] ProductNames { get; init; } = [];
    public ConfigLocation[] Configurations { get; init; } = [];
    public PortableLocation[] PortableLocations { get; init; } = [];
    public string[] RomExtensions { get; init; } = [];
    public string ArgumentPrefix { get; init; } = "";
    public LaunchArgumentMode LaunchArgumentMode { get; init; } = LaunchArgumentMode.RomPath;
    public string ArgumentTemplate { get; init; } = "{rom}";
    public RomMetadataRule? RomMetadata { get; init; }
    public bool DirectoryRoms { get; init; }
}

public enum LaunchArgumentMode
{
    RomPath,
    Template,
    Unsupported
}

public sealed class RomMetadataRule
{
    public string RelativePath { get; init; } = "";
    public string ElementName { get; init; } = "";
    public string[] RelativePaths { get; init; } = [];
    public bool SearchParentDirectories { get; init; }
}

public sealed class ConfigLocation
{
    public ConfigRoot Root { get; init; }
    public string RelativePath { get; init; } = "";
    public string[] Files { get; init; } = [];
    public string[] ContentKeys { get; init; } = [];
}

public sealed class PortableLocation
{
    public string Marker { get; init; } = "";
    public string ConfigPath { get; init; } = "";
    public string[] Files { get; init; } = [];
}

public enum ConfigRoot
{
    ExecutableDirectory,
    AppData,
    LocalAppData,
    Documents,
    UserProfile
}
