using ControllerCommon.Managers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;

namespace ControllerCommon.Platforms
{
    public enum PlatformType
    {
        Windows = 0,
        Steam = 1,
        Origin = 2,
        UbisoftConnect = 3,
        GOG = 4,
        RTSS = 5,
        HWiNFO = 6
    }

    public abstract class IPlatform : IDisposable
    {
        protected string Name;
        protected string ExecutableName;

        protected string InstallPath;
        protected string SettingsPath;
        protected string ExecutablePath;

        protected bool KeepAlive;
        protected bool IsStarting;

        protected Timer PlatformWatchdog;
        protected readonly object updateLock = new();

        private Process _Process;
        protected Process? Process
        {
            get
            {
                try
                {
                    if (_Process is not null && !_Process.HasExited)
                        return _Process;

                    var processes = Process.GetProcessesByName(Name);
                    if (processes.Length == 0)
                        return null;

                    _Process = processes.FirstOrDefault();
                    if (_Process.HasExited)
                        return null;

                    _Process.EnableRaisingEvents = true;
                    _Process.Exited += _Process_Exited;

                    return _Process;
                }
                catch
                {
                    return null;
                }
            }
        }

        private void _Process_Exited(object sender, EventArgs e)
        {
            _Process.Dispose();
            _Process = null;
        }

        public bool IsInstalled;
        public bool HasModules
        {
            get
            {
                foreach(var file in Modules)
                {
                    var filename = Path.Combine(InstallPath, file);
                    if (File.Exists(filename))
                        continue;
                    else
                        return false;
                }

                return true;
            }
        }

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
            return Process is not null && !Process.HasExited;
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
            catch (Win32Exception)
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

        public virtual void Dispose()
        {
            if (PlatformWatchdog is not null)
                PlatformWatchdog.Dispose();
        }
    }
}
