using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        private static SteamPlatform steamPlatform;
        private static bool IsInitialized;

        public static void Start()
        {
            steamPlatform = new();

            IsInitialized = true;

            LogManager.LogInformation("{0} has started", "PlatformManager");
        }

        public static Platform GetPlatform(Process proc)
        {
            if (!IsInitialized)
                return Platform.Windows;

            if (steamPlatform.IsRelated(proc))
                return steamPlatform.Platform;

            return Platform.Windows;
        }
    }
}
