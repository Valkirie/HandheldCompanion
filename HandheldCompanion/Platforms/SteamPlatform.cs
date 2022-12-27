using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Platforms
{
    public class SteamPlatform : IPlatform
    {
        public SteamPlatform()
        {
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

            // check if platform exists
            InstallPath = RegistryUtils.GetHKLM("SOFTWARE\\Wow6432Node\\Valve\\Steam", "InstallPath");
            if (Path.Exists(InstallPath))
                IsInstalled = true;
        }

        public bool IsRelated(Process proc)
        {
            foreach (ProcessModule module in proc.Modules)
                if (Modules.Contains(module.ModuleName))
                    return true;

            return false;
        }
    }
}
