using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        // gaming platforms
        private static SteamPlatform Steam;
        private static GOGGalaxy GOGGalaxy;
        private static UbisoftConnect UbisoftConnect;

        // misc platforms
        public static RTSS RTSS;

        private static bool IsInitialized;

        public static void Start()
        {
            // initialize supported gaming platforms
            Steam = new();
            GOGGalaxy = new();
            UbisoftConnect = new();

            if (Steam.IsInstalled)
            {
                // overwrite controller files
                foreach (var config in SteamPlatform.ControllerFiles)
                    Steam.OverwriteFile(config.Key, config.Value, true);
            }

            if (GOGGalaxy.IsInstalled)
            {
                // do something
            }

            if (UbisoftConnect.IsInstalled)
            {
                // do something
            }

            // initialize supported misc platforms
            RTSS = new();

            if (RTSS.IsInstalled)
            {
                int Limit = RTSS.GetTargetFPS();
                RTSS.SetTargetFPS(60);
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

            if (GOGGalaxy.IsInstalled)
            {
                // do something
            }

            if (UbisoftConnect.IsInstalled)
            {
                // do something
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
            else if (GOGGalaxy.IsRelated(proc))
                return GOGGalaxy.PlatformType;
            else if (UbisoftConnect.IsRelated(proc))
                return UbisoftConnect.PlatformType;
            else
                return PlatformType.Windows;
        }
    }
}
