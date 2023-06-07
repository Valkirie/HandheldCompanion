﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;

namespace HandheldCompanion.Platforms;

public class GOGGalaxy : IPlatform
{
    public GOGGalaxy()
    {
        Name = "GOG Galaxy";
        ExecutableName = "GalaxyClient.exe";

        // store specific modules
        Modules = new List<string>
        {
            "Galaxy.dll",
            "GalaxyClient.exe",
            "GalaxyClientService.exe"
        };

        // check if platform is installed
        InstallPath = RegistryUtils.GetString(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths", "client");
        if (Path.Exists(InstallPath))
        {
            // update paths
            SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"GOG.com\Galaxy\Configuration\config.json");
            ExecutablePath = Path.Combine(InstallPath, ExecutableName);

            // check executable
            IsInstalled = File.Exists(ExecutablePath);
        }

        PlatformType = PlatformType.GOG;
    }

    public override bool StartProcess()
    {
        if (!IsInstalled)
            return false;

        if (IsRunning())
            return false;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = ExecutablePath,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        return process is not null;
    }

    public override bool StopProcess()
    {
        if (!IsInstalled)
            return false;

        if (!IsRunning())
            return false;

        KillProcess();

        return true;
    }
}