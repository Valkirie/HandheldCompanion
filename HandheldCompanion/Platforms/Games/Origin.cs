using GameLib.Core;
using GameLib.Plugin.Origin;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HandheldCompanion.Platforms.Games;

public class Origin : IPlatform
{
    // GameLib
    private OriginLauncher originLauncher = new(new LauncherOptions());
    public override string Name => originLauncher.Name;
    public override string InstallPath => originLauncher.InstallDir;
    public override string ExecutablePath => originLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => originLauncher.IsInstalled;

    public Origin()
    {
        PlatformType = PlatformType.Origin;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        originLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return originLauncher.Start();
    }

    public override bool StopProcess()
    {
        originLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return originLauncher.Games;
    }

    public override Image GetLogo()
    {
        return originLauncher.Logo;
    }
}