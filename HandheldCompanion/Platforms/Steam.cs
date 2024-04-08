using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using HandheldCompanion.Utils;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HandheldCompanion.Platforms;

public class Steam : IPlatform
{
    public bool IsControllerDriverInstalled;

    private static readonly Regex ControllerBlacklistRegex =
        new("^(\\s*\"controller_blacklist\"\\s*\")([^\"]*)(\"\\s*)$");

    public static readonly Dictionary<string, byte[]> ControllerFiles = new()
    {
        { @"controller_base\desktop_neptune.vdf", Resources.empty_neptune },
        { @"controller_base\chord_neptune.vdf", Resources.chord_neptune },
        { @"controller_base\templates\controller_neptune_steamcontroller.vdf", Resources.empty_neptune },
    };

    public Steam()
    {
        PlatformType = PlatformType.Steam;

        Name = "Steam";
        ExecutableName = "steam.exe";
        RunningName = "steamwebhelper.exe";

        // store specific modules
        Modules = new List<string>
        {
            "steam.exe",
            "steamwebhelper.exe",
            "gameoverlayrenderer.dll",
            "gameoverlayrenderer64.dll",
            "steamclient.dll",
            "steamclient64.dll"
        };

        // check if platform is installed
        InstallPath = RegistryUtils.GetString(@"SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath");
        if (Path.Exists(InstallPath))
        {
            // update paths
            SettingsPath = Path.Combine(InstallPath, @"config\config.vdf");
            ExecutablePath = Path.Combine(InstallPath, ExecutableName);

            // check executable
            IsInstalled = File.Exists(ExecutablePath);
        }

        IsControllerDriverInstalled = HasXboxDriversInstalled();
    }

    public override bool Start()
    {
        // hook into current process
        if (IsRunning)
            Process.Exited += Process_Exited;

        ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;

        return base.Start();
    }

    public override bool Stop(bool kill = false)
    {
        ProcessManager.ProcessStarted -= ProcessManager_ProcessStarted;

        // restore files even if Steam is still running
        RestoreFiles();

        return base.Stop();
    }

    protected override void Process_Exited(object? sender, EventArgs e)
    {
        LogManager.LogDebug("Steam stopped, restoring files");
        RestoreFiles();
    }

    public bool HasXboxDriversInstalled()
    {
        return FilterDrivers
            .GetDeviceClassUpperFilters(DeviceClassIds.XnaComposite)
            .Any(f => f.Equals("steamxbox"));

        return RegistryUtils.SearchForKeyValue(@"SYSTEM\CurrentControlSet\Enum\ROOT\SYSTEM", "Service", "steamxbox");
    }

    public bool HasDesktopProfileApplied()
    {
        string filePath = ControllerFiles.Keys.FirstOrDefault();
        string configPath = Path.Combine(InstallPath, filePath);

        if (!File.Exists(configPath))
            return false;

        string configText = File.ReadAllText(configPath);
        string fileText = Encoding.UTF8.GetString(Resources.empty_neptune, 0, Resources.empty_neptune.Length);

        if (!configText.Equals(fileText, StringComparison.InvariantCultureIgnoreCase))
            return true;

        return false;
    }

    private void ReplaceFiles()
    {
        // overwrite controller files
        foreach (var config in ControllerFiles)
            OverwriteFile(config.Key, config.Value, true);
    }

    private void RestoreFiles()
    {
        // restore controller files
        foreach (var config in ControllerFiles)
            ResetFile(config.Key);
    }

    private async void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
    {
        if (!OnStartup && processEx.Executable == RunningName)
        {
            await Task.Delay(3000);
            ReplaceFiles();

            // hook into current process
            if (!Process.HasExited)
                Process.Exited += Process_Exited;
        }
    }

    public HashSet<string>? GetControllerBlacklist()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            foreach (var line in File.ReadLines(SettingsPath).Reverse())
            {
                var match = ControllerBlacklistRegex.Match(line);
                if (!match.Success)
                    continue;

                // matches `"controller_blacklist" "<value>"`
                var value = match.Groups[2].Captures[0].Value;
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            }

            return new HashSet<string>();
        }
        catch (DirectoryNotFoundException)
        {
            // Steam was installed, but got removed
            return null;
        }
        catch (IOException e)
        {
            LogManager.LogError("Failed to retrieve {0} controller blacklist. Error: {1}", PlatformType, e);
            return null;
        }
    }

    public bool UpdateControllerBlacklist(ushort vendorId, ushort productId, bool add)
    {
        if (IsRunning)
            return false;

        if (!FileUtils.IsFileWritable(SettingsPath))
            return false;

        try
        {
            var lines = File.ReadLines(SettingsPath).ToList();
            var id = string.Format("{0:x}/{1:x}", vendorId, productId);

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i] == "}")
                    if (add)
                    {
                        // append controller_blacklist
                        lines.Insert(i, string.Format("\t\"controller_blacklist\"\t\t\"{0}\"", id));
                        break;
                    }

                var match = ControllerBlacklistRegex.Match(lines[i]);
                if (!match.Success)
                    continue;

                var value = match.Groups[2].Captures[0].Value;
                var controllers = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

                if (add)
                    controllers.Add(id);
                else
                    controllers.Remove(id);

                lines[i] = string.Format("{0}{1}{2}",
                    match.Groups[1].Captures[0].Value,
                    string.Join(',', controllers),
                    match.Groups[3].Captures[0].Value
                );
                break;
            }

            File.WriteAllLines(SettingsPath, lines);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            // Steam was installed, but got removed
            return false;
        }
        catch (IOException e)
        {
            LogManager.LogError("Failed to update {0} controller blacklist. Error: {1}", PlatformType, e);
            return false;
        }
    }

    public bool? IsControllerBlacklisted(ushort vendorId, ushort productId)
    {
        var controllers = GetControllerBlacklist();
        if (controllers is null)
            return null;

        var id = string.Format("{0:x}/{1:x}", vendorId, productId);
        return controllers.Contains(id);
    }

    public override bool StartProcess()
    {
        if (!IsInstalled)
            return false;

        if (IsRunning)
            return false;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = ExecutablePath
        });

        return process is not null;
    }

    public override bool StopProcess()
    {
        if (!IsInstalled)
            return false;

        if (!IsRunning)
            return false;

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = ExecutablePath,
            ArgumentList = { "-shutdown" },
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        return process is not null;
    }
}