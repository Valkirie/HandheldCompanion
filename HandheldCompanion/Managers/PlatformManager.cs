using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        private static SteamPlatform Steam;

        private static bool IsInitialized;

        public static void Start()
        {
            // initialize supported platforms
            Steam = new();

            if (Steam.IsInstalled)
            {
                // overwrite controller files
                foreach (var config in SteamPlatform.ControllerFiles)
                    Steam.OverwriteFile(config.Key, config.Value, true);
            }

            IsInitialized = true;

            LogManager.LogInformation("{0} has started", "PlatformManager");
        }

        public static void Stop()
        {
            if (Steam.IsInstalled)
            {
                // restore controller files
                foreach (var config in SteamPlatform.ControllerFiles)
                    Steam.ResetFile(config.Key);
            }

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "PlatformManager");
        }

        public static PlatformType GetPlatform(Process proc)
        {
            if (!IsInitialized)
                return PlatformType.Windows;

            // is this process part of a specific platform
            if (Steam.IsRelated(proc))
                return Steam.PlatformType;
            else
                return PlatformType.Windows;
        }
    }
}
