using GameLib.Core;
using GameLib.Plugin.BattleNet;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HandheldCompanion.Platforms.Games;

public class BattleNet : IPlatform
{
    // GameLib
    private BattleNetLauncher battlenetLauncher = new(new LauncherOptions());
    public override string Name => battlenetLauncher.Name;
    public override string InstallPath => battlenetLauncher.InstallDir;
    public override string ExecutablePath => battlenetLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => battlenetLauncher.IsInstalled;

    public BattleNet()
    {
        PlatformType = PlatformType.BattleNet;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        battlenetLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return battlenetLauncher.Start();
    }

    public override bool StopProcess()
    {
        battlenetLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return battlenetLauncher.Games;
    }

    public override Image GetLogo()
    {
        return battlenetLauncher.Logo;
    }
}