using GameLib.Core;
using GameLib.Plugin.EA;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Platforms.Games;

public class EADesktop : IPlatform
{
    // GameLib
    private EALauncher eaLauncher = new(new LauncherOptions());
    public override string Name => eaLauncher.Name;
    public override string InstallPath => eaLauncher.InstallDir;
    public override string ExecutablePath => eaLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => eaLauncher.IsInstalled;

    public EADesktop()
    {
        PlatformType = GamePlatform.EADesktop;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        eaLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return eaLauncher.Start();
    }

    public override bool StopProcess()
    {
        eaLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return eaLauncher.Games.Where(game => !BlacklistIds.Contains(game.Id));
    }

    public override Image GetLogo()
    {
        return eaLauncher.Logo;
    }
}