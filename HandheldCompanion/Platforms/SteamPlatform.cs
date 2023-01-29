using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HandheldCompanion.Platforms
{
    public class SteamPlatform : IPlatform
    {
        private static readonly Regex ControllerBlacklistRegex = new Regex("^(\\s*\"controller_blacklist\"\\s*\")([^\"]*)(\"\\s*)$");
        public static readonly Dictionary<string, byte[]> ControllerFiles = new()
        {
                { @"controller_base\desktop_neptune.vdf", Resources.empty_neptune },
                { @"controller_base\chord_neptune.vdf", Resources.chord_neptune },
                { @"controller_base\templates\controller_neptune_steamcontroller.vdf", Resources.empty_neptune },
        };

        public SteamPlatform()
        {
            Name = "Steam";
            ExecutableName = "steam.exe";

            // store specific modules
            Modules = new List<string>()
            {
                "steam.exe",
                "steamwebhelper.exe",
                "gameoverlayrenderer.dll",
                "gameoverlayrenderer64.dll",
                "steamclient.dll",
                "steamclient64.dll",
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

            base.PlatformType = PlatformType.Steam;
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

                return new HashSet<String>();
            }
            catch (DirectoryNotFoundException)
            {
                // Steam was installed, but got removed
                return null;
            }
            catch (IOException e)
            {
                LogManager.LogError("Failed to retrieve {0} controller blacklist. Error: {1}", this.PlatformType, e);
                return null;
            }
        }

        public bool UpdateControllerBlacklist(ushort vendorId, ushort productId, bool add)
        {
            if (IsRunning())
                return false;

            if (!File.Exists(SettingsPath))
                return false;

            try
            {
                var lines = File.ReadLines(SettingsPath).ToList();
                var id = String.Format("{0:x}/{1:x}", vendorId, productId);

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i] == "}")
                    {
                        if (add)
                        {
                            // append controller_blacklist
                            lines.Insert(i, String.Format("\t\"controller_blacklist\"\t\t\"{0}\"", id));
                            break;
                        }
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

                    lines[i] = String.Format("{0}{1}{2}",
                        match.Groups[1].Captures[0].Value,
                        String.Join(',', controllers),
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
                LogManager.LogError("Failed to update {0} controller blacklist. Error: {1}", this.PlatformType, e);
                return false;
            }
        }

        public bool? IsControllerBlacklisted(ushort vendorId, ushort productId)
        {
            var controllers = GetControllerBlacklist();
            if (controllers is null)
                return null;

            var id = String.Format("{0:x}/{1:x}", vendorId, productId);
            return controllers.Contains(id);
        }

        public override bool IsRelated(Process proc)
        {
            foreach (ProcessModule module in proc.Modules)
                if (Modules.Contains(module.ModuleName))
                    return true;

            return false;
        }

        public override bool IsRunning()
        {
            return Process is not null;
        }

        public override bool Start()
        {
            if (!IsInstalled)
                return false;

            if (!IsRunning())
                return false;

            var process = Process.Start(new ProcessStartInfo()
            {
                FileName = ExecutablePath,
                // ArgumentList = { "-gamepadui" },
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return process is not null;
        }

        public override bool Stop()
        {
            if (!IsInstalled)
                return false;

            if (IsRunning())
                return false;

            var process = Process.Start(new ProcessStartInfo()
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
}
