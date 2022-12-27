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
        }

        public static Platform GetPlatform(Process proc)
        {
            if (!IsInitialized)
                return Platform.None;

            if (steamPlatform.IsRelated(proc))
                return steamPlatform.Platform;

            return Platform.None;
        }
    }
}
