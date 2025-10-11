using GameLib.Core;
using GameLib.Plugin.Gog;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Platforms.Games;

public class GOGGalaxy : IPlatform
{
    // GameLib
    private GogLauncher gogLauncher = new(new LauncherOptions());
    public override string Name => gogLauncher.Name;
    public override string InstallPath => gogLauncher.InstallDir;
    public override string ExecutablePath => gogLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => gogLauncher.IsInstalled;

    public GOGGalaxy()
    {
        PlatformType = GamePlatform.GOG;

        // refresh library
        Refresh();

        // store specific modules
        Modules =
        [
            "Galaxy.dll",
            "GalaxyClient.exe",
            "GalaxyClientService.exe"
        ];
    }

    public override void Refresh()
    {
        gogLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return gogLauncher.Start();
    }

    public override bool StopProcess()
    {
        gogLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return gogLauncher.Games.Where(game => !BlacklistIds.Contains(game.Id));
    }

    public override Image GetLogo()
    {
        return gogLauncher.Logo;
    }
}