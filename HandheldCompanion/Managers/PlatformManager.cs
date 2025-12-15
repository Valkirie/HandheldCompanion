using GameLib.Core;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Platforms.Games;
using HandheldCompanion.Platforms.Misc;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers;

public class PlatformManager : IManager
{
    public static List<IPlatform> GamingPlatforms;
    public static List<IPlatform> MiscPlatforms;
    public static List<IPlatform> AllPlatforms;

    // gaming platforms
    public static Steam Steam;
    public static GOGGalaxy GOGGalaxy;
    public static UbisoftConnect UbisoftConnect;
    public static BattleNet BattleNet;
    public static Origin Origin;
    public static Epic Epic;
    public static RiotGames RiotGames;
    public static Rockstar Rockstar;
    public static EADesktop EADesktop;

    // misc platforms
    public static RTSSPlatform RTSS;
    public static LibreHardwarePlatform LibreHardware;
    public static WindowsPlatform WindowsPlatform;

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

        // initialize misc platforms
        RTSS = new RTSSPlatform();
        LibreHardware = new LibreHardwarePlatform();
        WindowsPlatform = new WindowsPlatform();

        // populate lists
        GamingPlatforms = new() { Steam, GOGGalaxy, UbisoftConnect, BattleNet, Origin, Epic, RiotGames, Rockstar, EADesktop };
        MiscPlatforms = new() { RTSS, LibreHardware, WindowsPlatform };
        AllPlatforms = new(GamingPlatforms.Concat(MiscPlatforms));

        // start platforms
        foreach (IPlatform platform in AllPlatforms)
        {
            if (platform.IsInstalled)
                platform.Start();
        }

        base.Start();
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
        foreach (IPlatform platform in GamingPlatforms)
            if (platform.IsRelated(proc))
                return platform.PlatformType;

        return GamePlatform.Generic;
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

        return games;
    }
}