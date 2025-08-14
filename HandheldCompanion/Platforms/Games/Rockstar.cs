using GameLib.Core;
using GameLib.Plugin.RiotGames;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HandheldCompanion.Platforms.Games;

public class Rockstar : IPlatform
{
    // GameLib
    private RiotGamesLauncher rockstartLauncher = new(new LauncherOptions());
    public override string Name => rockstartLauncher.Name;
    public override string InstallPath => rockstartLauncher.InstallDir;
    public override string ExecutablePath => rockstartLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => rockstartLauncher.IsInstalled;

    public Rockstar()
    {
        PlatformType = PlatformType.Rockstar;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        rockstartLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return rockstartLauncher.Start();
    }

    public override bool StopProcess()
    {
        rockstartLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return rockstartLauncher.Games;
    }

    public override Image GetLogo()
    {
        return rockstartLauncher.Logo;
    }
}