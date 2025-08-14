using GameLib.Core;
using GameLib.Plugin.Ubisoft;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HandheldCompanion.Platforms.Games;

public class UbisoftConnect : IPlatform
{
    // GameLib
    private UbisoftLauncher ubisoftLauncher = new(new LauncherOptions());
    public override string Name => ubisoftLauncher.Name;
    public override string InstallPath => ubisoftLauncher.InstallDir;
    public override string ExecutablePath => ubisoftLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => ubisoftLauncher.IsInstalled;

    public UbisoftConnect()
    {
        PlatformType = PlatformType.UbisoftConnect;

        // refresh library
        Refresh();

        // store specific modules
        Modules =
        [
            "UbisoftConnect.exe",
            "UbisoftExtension.exe",
            "UbisoftGameLauncher.exe",
            "UbisoftGameLauncher64.exe",
            "uplay_r1_loader64.dll",
            "uplay_r164.dll"
        ];
    }

    public override void Refresh()
    {
        ubisoftLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return ubisoftLauncher.Start();
    }

    public override bool StopProcess()
    {
        ubisoftLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return ubisoftLauncher.Games;
    }

    public override Image GetLogo()
    {
        return ubisoftLauncher.Logo;
    }
}