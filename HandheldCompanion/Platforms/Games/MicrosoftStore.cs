using GameFinder.StoreHandlers.Xbox;
using GameLib.Core;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using NexusMods.Paths;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HandheldCompanion.Platforms.Games;

public class MicrosoftStore : IPlatform
{
    private static readonly string WindowsStorePackageName = "Microsoft.WindowsStore_";
    private static readonly string[] IgnoredGameNameKeywords =
    [
        "launcher",
        "gaming services",
        "game save"
    ];

    private static readonly string[] IgnoredExecutableKeywords =
    [
        "crash",
        "error",
        "installer",
        "patch",
        "redist",
        "setup",
        "tool",
        "unins",
        "updater"
    ];

    private readonly XboxHandler xboxHandler = new(FileSystem.Shared);
    private List<MicrosoftStoreGame> games = [];
    private readonly Lazy<Image> logo;

    public override string Name => "Microsoft Store";
    public override string ExecutableName => "XboxPcApp.exe";
    public override bool IsInstalled => games.Count != 0;

    public MicrosoftStore()
    {
        PlatformType = GamePlatform.MicrosoftStore;
        logo = new Lazy<Image>(CreateLogo);
        Refresh();
    }

    public override void Refresh()
    {
        List<MicrosoftStoreGame> refreshedGames = [];

        foreach (var result in xboxHandler.FindAllGames())
        {
            result.Switch(
                game =>
                {
                    MicrosoftStoreGame? parsedGame = CreateGame(game);
                    if (parsedGame is not null && !BlacklistIds.Contains(parsedGame.Id))
                        refreshedGames.Add(parsedGame);
                },
                error => LogManager.LogWarning("Failed to inspect Microsoft Store game: {0}", error.Message));
        }

        games = refreshedGames
            .GroupBy(game => game.Id, StringComparer.InvariantCultureIgnoreCase)
            .Select(group => group.First())
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public override IEnumerable<IGame> GetGames()
    {
        return games;
    }

    public override Image GetLogo()
    {
        return logo.Value;
    }

    private static Image CreateLogo()
    {
        try
        {
            string? packagePath = GetWindowsStorePackagePath();
            if (!string.IsNullOrEmpty(packagePath))
            {
                foreach (string candidate in GetLogoCandidates(packagePath))
                {
                    if (!File.Exists(candidate))
                        continue;

                    using FileStream stream = File.OpenRead(candidate);
                    using Image image = Image.FromStream(stream);
                    return new Bitmap(image);
                }
            }
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("Failed to resolve Microsoft Store logo: {0}", ex.Message);
        }

        return SystemIcons.Application.ToBitmap();
    }

    private static string? GetWindowsStorePackagePath()
    {
        try
        {
            string windowsAppsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (!Directory.Exists(windowsAppsPath))
                return null;

            return Directory
                .EnumerateDirectories(windowsAppsPath, $"{WindowsStorePackageName}*")
                .OrderByDescending(path => path, StringComparer.InvariantCultureIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("Failed to locate Microsoft Store package path: {0}", ex.Message);
            return null;
        }
    }

    private static IEnumerable<string> GetLogoCandidates(string packagePath)
    {
        string manifestPath = Path.Combine(packagePath, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
            return Enumerable.Empty<string>();

        try
        {
            XDocument document = XDocument.Load(manifestPath);

            IEnumerable<string> logoPaths = document
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("VisualElements", StringComparison.InvariantCultureIgnoreCase))
                .SelectMany(element => new[]
                {
                    element.Attribute("Square44x44Logo")?.Value,
                    element.Attribute("Square150x150Logo")?.Value
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>();

            return logoPaths
                .SelectMany(path => ExpandScaleQualifiedPaths(packagePath, path))
                .Distinct(StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("Failed to parse Microsoft Store manifest for logo: {0}", ex.Message);
            return Enumerable.Empty<string>();
        }
    }

    private static IEnumerable<string> ExpandScaleQualifiedPaths(string packagePath, string relativePath)
    {
        string normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string directoryPath = Path.Combine(packagePath, Path.GetDirectoryName(normalizedPath) ?? string.Empty);
        string extension = Path.GetExtension(normalizedPath);
        string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
        string exactPath = Path.Combine(packagePath, normalizedPath);

        if (File.Exists(exactPath))
            yield return exactPath;

        foreach (string scale in new[] { "scale-100", "scale-125", "scale-150", "scale-200", "scale-400" })
        {
            string scaleQualifiedPath = Path.Combine(directoryPath, $"{fileName}.{scale}{extension}");
            if (File.Exists(scaleQualifiedPath))
                yield return scaleQualifiedPath;
        }

        if (!Directory.Exists(directoryPath))
            yield break;

        foreach (string candidate in Directory
            .EnumerateFiles(directoryPath, $"{fileName}*{extension}", SearchOption.TopDirectoryOnly)
            .OrderByDescending(GetLogoCandidateScore)
            .ThenBy(path => path, StringComparer.InvariantCultureIgnoreCase))
        {
            yield return candidate;
        }
    }

    private static int GetLogoCandidateScore(string path)
    {
        string fileName = Path.GetFileName(path);

        if (fileName.Contains("scale-400", StringComparison.InvariantCultureIgnoreCase))
            return 400;
        if (fileName.Contains("scale-200", StringComparison.InvariantCultureIgnoreCase))
            return 200;
        if (fileName.Contains("scale-150", StringComparison.InvariantCultureIgnoreCase))
            return 150;
        if (fileName.Contains("scale-125", StringComparison.InvariantCultureIgnoreCase))
            return 125;
        if (fileName.Contains("scale-100", StringComparison.InvariantCultureIgnoreCase))
            return 100;
        if (fileName.Contains("targetsize-", StringComparison.InvariantCultureIgnoreCase))
            return 50;

        return 0;
    }

    private static MicrosoftStoreGame? CreateGame(XboxGame game)
    {
        string installDir = NormalizePath(game.Path.ToString());
        if (!Directory.Exists(installDir))
            return null;

        MicrosoftGameConfig config = ReadGameConfig(installDir, game.DisplayName);
        string displayName = string.IsNullOrWhiteSpace(config.DisplayName) ? game.DisplayName : config.DisplayName;

        if (IsIgnoredGame(displayName))
            return null;

        List<string> executables = GetExecutables(installDir, config)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();

        if (executables.Count == 0)
            return null;

        string primaryExecutable = executables.FirstOrDefault(executable => !IsIgnoredExecutable(executable)) ?? executables[0];
        DateTime installDate = GetInstallDate(primaryExecutable, installDir);

        return new MicrosoftStoreGame(
            config.IdentityName ?? game.Id.Value,
            displayName,
            installDir,
            primaryExecutable,
            executables,
            installDate);
    }

    private static MicrosoftGameConfig ReadGameConfig(string installDir, string fallbackDisplayName)
    {
        string configPath = Path.Combine(installDir, "MicrosoftGame.config");
        if (File.Exists(configPath))
        {
            try
            {
                XDocument document = XDocument.Load(configPath);
                XElement? root = document.Root;

                return new MicrosoftGameConfig(
                    root?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Identity", StringComparison.InvariantCultureIgnoreCase))?.Attribute("Name")?.Value,
                    root?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("ShellVisuals", StringComparison.InvariantCultureIgnoreCase))?.Attribute("DefaultDisplayName")?.Value ?? fallbackDisplayName,
                    root?
                        .Descendants()
                        .Where(element => element.Name.LocalName.Equals("Executable", StringComparison.InvariantCultureIgnoreCase))
                        .Select(element => element.Attribute("Name")?.Value)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Cast<string>()
                        .ToList() ?? []);
            }
            catch (Exception ex)
            {
                LogManager.LogDebug("Failed to parse MicrosoftGame.config from {0}: {1}", installDir, ex.Message);
            }
        }

        return new MicrosoftGameConfig(null, fallbackDisplayName, []);
    }

    private static IEnumerable<string> GetExecutables(string installDir, MicrosoftGameConfig config)
    {
        if (config.Executables.Count != 0)
        {
            foreach (string executable in config.Executables)
            {
                string fullPath = NormalizePath(Path.Combine(installDir, executable));
                if (File.Exists(fullPath))
                    yield return fullPath;
            }
        }

        IEnumerable<string> discoveredExecutables = Enumerable.Empty<string>();

        try
        {
            discoveredExecutables = Directory.EnumerateFiles(installDir, "*.exe", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("Failed to enumerate Microsoft Store executables from {0}: {1}", installDir, ex.Message);
        }

        foreach (string executable in discoveredExecutables.Where(executable => !IsIgnoredExecutable(executable)))
            yield return NormalizePath(executable);
    }

    private static string NormalizePath(string path)
    {
        string normalizedPath = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        try
        {
            return Path.GetFullPath(normalizedPath);
        }
        catch
        {
            return normalizedPath;
        }
    }

    private static bool IsIgnoredGame(string name)
    {
        return IgnoredGameNameKeywords.Any(keyword => name.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
    }

    private static bool IsIgnoredExecutable(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        return IgnoredExecutableKeywords.Any(keyword => fileName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
    }

    private static DateTime GetInstallDate(string executable, string installDir)
    {
        try
        {
            if (File.Exists(executable))
                return File.GetCreationTime(executable);
        }
        catch { }

        try
        {
            if (Directory.Exists(installDir))
                return Directory.GetCreationTime(installDir);
        }
        catch { }

        return DateTime.MinValue;
    }

    private sealed record MicrosoftGameConfig(string? IdentityName, string DisplayName, IReadOnlyList<string> Executables);
}

public sealed class MicrosoftStoreGame : IGame
{
    private static readonly Guid MicrosoftStoreLauncherId = new("0A1B3D6F-2927-4F7F-A0E7-9A8AEB39C3A3");
    private readonly Lazy<Icon> executableIcon;

    public MicrosoftStoreGame(string id, string name, string installDir, string executable, IReadOnlyCollection<string> executables, DateTime installDate)
    {
        Id = id;
        Name = name;
        InstallDir = installDir;
        Executable = executable;
        Executables = executables;
        WorkingDir = Path.GetDirectoryName(executable) ?? installDir;
        LaunchString = executable;
        InstallDate = installDate;
        executableIcon = new Lazy<Icon>(CreateExecutableIcon);
    }

    public string Id { get; }
    public Guid LauncherId => MicrosoftStoreLauncherId;
    public string Name { get; }
    public string InstallDir { get; }
    public string Executable { get; }
    public Icon ExecutableIcon => executableIcon.Value;
    public IEnumerable<string> Executables { get; }
    public string WorkingDir { get; }
    public string LaunchString { get; }
    public DateTime InstallDate { get; }
    public bool IsRunning => Executables.Any(path => ProcessUtils.GetProcessesByExecutable(Path.GetFileName(path)).Any());

    private Icon CreateExecutableIcon()
    {
        try
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Executable);
            if (icon is not null)
                return (Icon)icon.Clone();
        }
        catch { }

        return SystemIcons.Application;
    }
}
