using GameLib.Core;
using GameLib.Plugin.RiotGames;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Platforms.Games;

public class RiotGames : IPlatform
{
    // GameLib
    private RiotGamesLauncher riotLauncher = new(new LauncherOptions());
    public override string Name => riotLauncher.Name;
    public override string InstallPath => riotLauncher.InstallDir;
    public override string ExecutablePath => riotLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => riotLauncher.IsInstalled;

    public RiotGames()
    {
        PlatformType = GamePlatform.RiotGames;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        riotLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return riotLauncher.Start();
    }

    public override bool StopProcess()
    {
        riotLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return riotLauncher.Games.Where(game => !BlacklistIds.Contains(game.Id));
    }

    public override Image GetLogo()
    {
        return riotLauncher.Logo;
    }
}