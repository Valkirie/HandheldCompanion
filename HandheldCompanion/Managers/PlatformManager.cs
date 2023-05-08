using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        // gaming platforms
        private static SteamPlatform Steam = new();
        private static GOGGalaxy GOGGalaxy = new();
        private static UbisoftConnect UbisoftConnect = new();

        // misc platforms
        public static RTSS RTSS = new();
        public static HWiNFO HWiNFO = new();

        private static bool IsInitialized;

        public static void Start()
        {
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

            if (RTSS.IsInstalled)
            {
                // do something
            }

            if (HWiNFO.IsInstalled)
            {
                // do something
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

                Steam.Dispose();
            }

            if (GOGGalaxy.IsInstalled)
            {
                GOGGalaxy.Dispose();
            }

            if (UbisoftConnect.IsInstalled)
            {
                UbisoftConnect.Dispose();
            }

            if (RTSS.IsInstalled)
            {
                RTSS.Dispose();
            }

            if (HWiNFO.IsInstalled)
            {
                HWiNFO.Dispose();
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
