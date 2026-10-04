using GameLib.Core;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Discovery;
using HandheldCompanion.Platforms.Games;
using HandheldCompanion.Platforms.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Managers;

public class PlatformManager : IManager
{
    private static readonly IReadOnlyDictionary<string, GamePlatform> ScanPlatformAliases = new Dictionary<string, GamePlatform>(StringComparer.OrdinalIgnoreCase)
    {
        ["BattleNet"] = GamePlatform.BattleNet,
        ["Epic"] = GamePlatform.Epic,
        ["GOG"] = GamePlatform.GOG,
        ["Origin"] = GamePlatform.Origin,
        ["EA Desktop"] = GamePlatform.EADesktop,
        ["Riot"] = GamePlatform.RiotGames,
        ["Rockstar"] = GamePlatform.Rockstar,
        ["Steam"] = GamePlatform.Steam,
        ["Microsoft Store"] = GamePlatform.MicrosoftStore,
        ["Ubisoft"] = GamePlatform.UbisoftConnect
    };

    public static List<IPlatform> GamingPlatforms = null!;
    public static List<IPlatform> MiscPlatforms = null!;
    public static List<IPlatform> AllPlatforms = null!;

    // gaming platforms
    public static Steam Steam = null!;
    public static GOGGalaxy GOGGalaxy = null!;
    public static UbisoftConnect UbisoftConnect = null!;
    public static BattleNet BattleNet = null!;
    public static Origin Origin = null!;
    public static Epic Epic = null!;
    public static RiotGames RiotGames = null!;
    public static Rockstar Rockstar = null!;
    public static EADesktop EADesktop = null!;
    public static MicrosoftStore MicrosoftStore = null!;

    // misc platforms
    public static RTSSPlatform RTSS = null!;
    public static LibreHardwarePlatform LibreHardware = null!;
    public static WindowsPlatform WindowsPlatform = null!;

    public PlatformManager()
    { }

    public override void Start()
    {
        if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
            return;

        base.PrepareStart();

        // initialize gaming platforms
        Steam = new Steam();
        GOGGalaxy = new GOGGalaxy();
        UbisoftConnect = new UbisoftConnect();
        BattleNet = new BattleNet();
        Origin = new Origin();
        Epic = new Epic();
        RiotGames = new RiotGames();
        Rockstar = new Rockstar();
        EADesktop = new EADesktop();
        MicrosoftStore = new MicrosoftStore();

        // initialize misc platforms
        RTSS = new RTSSPlatform();
        LibreHardware = new LibreHardwarePlatform();
        WindowsPlatform = new WindowsPlatform();

        // populate lists
        GamingPlatforms = new() { Steam, GOGGalaxy, UbisoftConnect, BattleNet, Origin, Epic, RiotGames, Rockstar, EADesktop, MicrosoftStore };
        MiscPlatforms = new() { RTSS, LibreHardware, WindowsPlatform };
        AllPlatforms = new(GamingPlatforms.Concat(MiscPlatforms));

        // start platforms
        foreach (IPlatform platform in AllPlatforms)
        {
            if (platform.IsInstalled)
                platform.Start();
        }

        base.Start();

        // Update platforms for any processes that were created during initialization
        ProcessManager.UpdatePlatformForProcess();
    }

    public override void Stop()
    {
        if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
            return;

        base.PrepareStop();

        // stop platforms
        foreach (IPlatform platform in AllPlatforms)
        {
            if (platform.IsInstalled)
            {
                bool kill = true;

                if (platform is RTSSPlatform)
                    kill = ManagerFactory.settingsManager.GetBoolean("PlatformRTSSEnabled");
                else if (platform is LibreHardwarePlatform)
                    kill = false;

                platform.Stop(kill);
            }
        }

        base.Stop();
    }

    public static GamePlatform GetPlatform(ProcessEx proc)
    {
        EmulatorDefinition? emulator = EmulatorDefinitions.All.FirstOrDefault(definition =>
            definition.Executables.Contains(proc.Executable, StringComparer.OrdinalIgnoreCase));
        if (emulator is not null)
            return emulator.PlatformType;

        if (ManagerFactory.platformManager.Status == ManagerStatus.Initialized)
            foreach (IPlatform platform in GamingPlatforms)
                if (platform.IsRelated(proc))
                    return platform.PlatformType;

        return GamePlatform.Generic;
    }

    public static GamePlatform GetPlatform(Profile profile)
    {
        string[] paths = [profile.Path, profile.LaunchString, .. profile.Executables];

        EmulatorDefinition? emulator = EmulatorDefinitions.All.FirstOrDefault(definition =>
            paths.Any(path => !string.IsNullOrWhiteSpace(path) && definition.Executables.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)));
        if (emulator is not null)
            return emulator.PlatformType;

        foreach (IPlatform platform in GamingPlatforms /* ?? [] */)
        {
            if (paths.Any(path => !string.IsNullOrWhiteSpace(path) && platform.IsRelated(path)))
                return platform.PlatformType;
        }

        return GamePlatform.Generic;
    }

    public static string GetPlatformColor(GamePlatform platform)
    {
        IPlatform? registeredPlatform = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (registeredPlatform is not null)
            return registeredPlatform.PlatformColor;

        return EmulatorDefinitions.All.FirstOrDefault(definition => definition.PlatformType == platform)?.PlatformColor ?? "#666666";
    }

    public static string GetPlatformGlyph(GamePlatform platform)
    {
        IPlatform? registeredPlatform = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (registeredPlatform is not null)
            return registeredPlatform.PlatformGlyph;

        return EmulatorDefinitions.All.FirstOrDefault(definition => definition.PlatformType == platform)?.PlatformGlyph ?? "\uF712";
    }

    public static string GetPlatformFont(GamePlatform platform)
    {
        IPlatform? registeredPlatform = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (registeredPlatform is not null)
            return registeredPlatform.PlatformFont;

        return EmulatorDefinitions.All.FirstOrDefault(definition => definition.PlatformType == platform)?.PlatformFont ?? "Simple Icons Fit";
    }

    public static double GetPlatformFontSize(GamePlatform platform)
    {
        IPlatform? registeredPlatform = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (registeredPlatform is not null)
            return registeredPlatform.PlatformFontSize;

        return EmulatorDefinitions.All.FirstOrDefault(definition => definition.PlatformType == platform)?.PlatformFontSize ?? 22;
    }

    public static string GetPlatformName(GamePlatform platform)
    {
        IPlatform? registeredPlatform = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (registeredPlatform is not null && !string.IsNullOrWhiteSpace(registeredPlatform.Name))
            return registeredPlatform.Name;

        return EmulatorDefinitions.All.FirstOrDefault(definition => definition.PlatformType == platform)?.Name ?? platform.ToString();
    }

    public static IEnumerable<IGame> GetGamesForScanTarget(string? target)
    {
        if (string.Equals(target, "All", StringComparison.OrdinalIgnoreCase))
            return GetGames(GamePlatform.All);

        if (string.Equals(target, "Emulators", StringComparison.OrdinalIgnoreCase))
            return EmulatorDiscoveryService.Discover();

        if (target is not null && EmulatorDefinitions.All.Any(definition =>
            string.Equals(definition.Id, target, StringComparison.OrdinalIgnoreCase)))
            return EmulatorDiscoveryService.DiscoverByDefinition(target);

        if (target is not null && ScanPlatformAliases.TryGetValue(target, out GamePlatform aliasPlatform))
            return GetGames(aliasPlatform);

        if (Enum.TryParse(target, ignoreCase: true, out GamePlatform platform) && platform != GamePlatform.Generic)
            return GetGames(platform);

        return [];
    }

    public static Image? GetPlatformLogo(GamePlatform platform)
    {
        Image? logo = AllPlatforms?.FirstOrDefault(candidate => candidate.PlatformType == platform)?.GetLogo();
        if (logo is not null)
            return logo;

        EmulatorDefinition? definition = EmulatorDefinitions.All
            .FirstOrDefault(candidate => candidate.PlatformType == platform);
        if (definition is null)
            return null;

        foreach (Profile profile in ManagerFactory.profileManager.GetProfiles(true))
        {
            string[] paths = [profile.Path, profile.LaunchString, .. profile.Executables];
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) ||
                    !definition.Executables.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase) ||
                    !File.Exists(path))
                    continue;

                try
                {
                    using Icon? icon = Icon.ExtractAssociatedIcon(path);
                    return icon?.ToBitmap();
                }
                catch (ArgumentException) { }
                catch (ExternalException) { }
            }
        }

        return null;
    }

    public static IEnumerable<IGame> GetGames(GamePlatform gamePlatform)
    {
        List<IGame> games = new List<IGame>();

        foreach (IPlatform platform in GamingPlatforms)
        {
            if (!gamePlatform.HasFlag(platform.PlatformType))
                continue;

            platform.Refresh();
            games.AddRange(platform.GetGames());
        }

        games.AddRange(DiscoverGames(gamePlatform));

        return games;
    }

    public static IEnumerable<IGame> DiscoverGames(GamePlatform gamePlatform = GamePlatform.All)
    {
        return EmulatorDiscoveryService.Discover(gamePlatform);
    }
}