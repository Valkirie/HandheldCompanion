using GameLib.Core;
using GameLib.Plugin.Epic;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace HandheldCompanion.Platforms.Games;

public class Epic : IPlatform
{
    // GameLib
    private EpicLauncher epicLauncher = new(new LauncherOptions());
    public override string Name => epicLauncher.Name;
    public override string InstallPath => epicLauncher.InstallDir;
    public override string ExecutablePath => epicLauncher.Executable;
    public override string ExecutableName => Path.GetFileName(ExecutablePath);
    public override bool IsInstalled => epicLauncher.IsInstalled;

    public Epic()
    {
        PlatformType = PlatformType.Epic;

        // refresh library
        Refresh();
    }

    public override void Refresh()
    {
        epicLauncher?.Refresh();
    }

    public override bool StartProcess()
    {
        return epicLauncher.Start();
    }

    public override bool StopProcess()
    {
        epicLauncher.Stop();
        return true;
    }

    public override IEnumerable<IGame> GetGames()
    {
        return epicLauncher.Games;
    }

    public override Image GetLogo()
    {
        return epicLauncher.Logo;
    }
}