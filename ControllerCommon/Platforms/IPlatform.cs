using ControllerCommon.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ControllerCommon.Platforms
{
    public enum PlatformType
    {
        Windows = 0,
        Steam = 1,
        Origin = 2,
        UbisoftConnect = 3,
        GOG = 4,
        RTSS = 5
    }

    public abstract class IPlatform
    {
        protected string Name;
        protected string ExecutableName;

        protected string InstallPath;
        protected string SettingsPath;
        protected string ExecutablePath;

        protected Process? Process
        {
            get
            {
                try
                {
                    var processes = Process.GetProcessesByName(Name);
                    if (processes.Length == 0)
                        return null;

                    var process = processes.FirstOrDefault();
                    if (process.HasExited)
                        return null;

                    return process;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool IsInstalled;

        public PlatformType PlatformType;

        protected List<string> Modules = new();

        public string GetName()
        {
            return Name;
        }

        public string GetInstallPath()
        {
            return InstallPath;
        }

        public string GetSettingsPath()
        {
            return SettingsPath;
        }

        public virtual string GetSetting(string key)
        {
            return string.Empty;
        }

        public virtual bool IsRelated(Process proc)
        {
            try
            {
                foreach (ProcessModule module in proc.Modules)
                    if (Modules.Contains(module.ModuleName))
                        return true;
            }
            catch (Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return false;
        }

        public virtual bool IsRunning()
        {
            return Process is not null;
        }

        public virtual bool Start()
        {
            return false;
        }

        public virtual bool Stop()
        {
            return false;
        }

        public bool Kill()
        {
            var process = Process;
            if (process is null)
                return true;

            try
            {
                using (process) { process.Kill(); }
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        public bool IsFileOverwritten(string FilePath, byte[] content)
        {
            try
            {
                var configPath = Path.Combine(InstallPath, FilePath);
                if (!File.Exists(configPath))
                    return false;

                byte[] diskContent = File.ReadAllBytes(configPath);
                return content.SequenceEqual(diskContent);
            }
            catch (DirectoryNotFoundException)
            {
                // Steam was installed, but got removed
                return false;
            }
            catch (IOException e)
            {
                LogManager.LogError("Couldn't locate {0} configuration file", this.PlatformType);
                return false;
            }
        }

        public bool ResetFile(string FilePath)
        {
            try
            {
                var configPath = Path.Combine(InstallPath, FilePath);
                if (!File.Exists(configPath))
                    return false;

                var origPath = $"{configPath}.orig";
                if (!File.Exists(origPath))
                    return false;

                File.Copy(origPath, configPath, true);
                return true;
            }
            catch (FileNotFoundException e)
            {
                // File was not found (which is valid as it might be before first start of the application)
                LogManager.LogError("Couldn't locate {0} configuration file", this.PlatformType);
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                // Steam was installed, but got removed
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch (IOException e)
            {
                LogManager.LogError("Failed to overwrite {0} configuration file", this.PlatformType);
                return false;
            }
        }

        public bool OverwriteFile(string FilePath, byte[] content, bool backup)
        {
            try
            {
                var configPath = Path.Combine(InstallPath, FilePath);
                if (!File.Exists(configPath))
                    return false;

                // file has already been overwritten
                if (IsFileOverwritten(FilePath, content))
                    return false;

                if (backup)
                {
                    var origPath = $"{configPath}.orig";
                    File.Copy(configPath, origPath, true);
                }

                File.WriteAllBytes(configPath, content);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                // Steam was installed, but got removed
                return false;
            }
            catch (IOException e)
            {
                LogManager.LogError("Failed to overwrite {0} configuration file", this.PlatformType);
                return false;
            }
        }
    }
}
