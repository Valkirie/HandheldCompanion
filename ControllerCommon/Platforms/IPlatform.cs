using System.Collections.Generic;

namespace ControllerCommon.Platforms
{
    public enum Platform
    {
        None = 0,
        Steam = 1,
        Origin = 2,
        UbisoftConnect = 3,
        GOG = 4
    }

    public abstract class IPlatform
    {
        protected string Name;
        protected string InstallPath;
        public bool IsInstalled;

        public Platform Platform;

        protected List<string> Modules = new List<string>();

        public string GetName() { return Name; }
        public string GetPath() { return InstallPath; }

        public virtual string GetSetting(string key) { return string.Empty; }
    }
}
