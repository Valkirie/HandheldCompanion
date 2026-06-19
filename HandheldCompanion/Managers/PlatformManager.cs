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
        if (ManagerFactory.platformManager.Status == ManagerStatus.Initialized)
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