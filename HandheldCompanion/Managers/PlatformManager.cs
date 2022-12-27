using ControllerCommon.Utils;
using HandheldCompanion.Platforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using ControllerCommon.Platforms;

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
