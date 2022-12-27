using ControllerCommon.Platforms;
using HandheldCompanion.Platforms;
using System.Diagnostics;

namespace HandheldCompanion.Managers
{
    public static class PlatformManager
    {
        private static SteamPlatform steamPlatform;

        public static void Start()
        {
            steamPlatform = new();
        }

        public static Platform GetPlatform(Process proc)
        {
            if (steamPlatform.IsRelated(proc))
                return steamPlatform.Platform;

            return Platform.None;
        }
    }
}
