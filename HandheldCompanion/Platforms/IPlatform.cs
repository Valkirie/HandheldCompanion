using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Platforms;

public enum PlatformType
{
    Windows = 0,
    Steam = 1,
    Origin = 2,
    UbisoftConnect = 3,
    GOG = 4,
    RTSS = 5
}

public enum PlatformStatus
{
    None = 0,
    Ready = 1,
    Started = 2,
    Stopped = 3,
    Stalled = 4,
    Starting = 5,
    Stopping = 6,
}

public abstract class IPlatform : IDisposable
{
    protected readonly object updateLock = new();

    private bool _IsInstalled;

    private Process _Process;
    protected string ExecutableName;
    protected string RunningName;
    protected string ExecutablePath;
    protected Version ExpectedVersion;

    protected string InstallPath;
    protected bool IsStarting;

    protected bool KeepAlive;
    protected int MaxTentative = 3;

    protected List<string> Modules = new();
    protected string Name;

    public PlatformType PlatformType;

    protected Timer PlatformWatchdog;
    protected string SettingsPath;
    public PlatformStatus Status;

    protected int Tentative;
    protected string Url;

    protected FileSystemWatcher systemWatcher;

    protected Process Process
    {
        get
        {
            try
            {
                if (_Process is not null)
                    return _Process;

                var processes = ProcessUtils.GetProcessesByExecutable(RunningName);
                if (processes.Length == 0)
                    return null;

                _Process = processes.FirstOrDefault();
                _Process.EnableRaisingEvents = true;

                SetStatus(PlatformStatus.Started);

                _Process.Exited += _Process_Exited;

                return _Process;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool IsInstalled
    {
        get => _IsInstalled;

        set
        {
            _IsInstalled = value;

            // raise event
            if (value)
                SetStatus(PlatformStatus.Ready);
            else
                SetStatus(PlatformStatus.Stalled);
        }
    }

    public bool IsRunning
    {
        get
        {
            try
            {
                if (Process is null)
                    return false;

                SetStatus(PlatformStatus.Started);
                return !Process.HasExited;
            }
            catch
            {
            }

            return false;
        }
    }

    public bool HasModules
    {
        get
        {
            foreach (var file in Modules)
            {
                var filename = Path.Combine(InstallPath, file);
                if (File.Exists(filename))
                    continue;
                return false;
            }

            return true;
        }
    }

    public virtual void Dispose()
    {
        if (PlatformWatchdog is not null)
        {
            PlatformWatchdog.Stop();
            PlatformWatchdog.Dispose();
            PlatformWatchdog = null;
        }

        GC.SuppressFinalize(this);
    }

    private void _Process_Exited(object sender, EventArgs e)
    {
        if (_Process is null)
            return;

        SetStatus(PlatformStatus.Stopped);

        _Process = null;
    }

    protected void SetStatus(PlatformStatus status)
    {
        if (Status == status)
            return;

        Status = status;
        Updated?.Invoke(status);
    }

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

    public virtual bool Start()
    {
        KeepAlive = true;

        // start watchdog
        if (PlatformWatchdog is not null)
            PlatformWatchdog.Start();

        return true;
    }

    public virtual bool Stop(bool kill = false)
    {
        KeepAlive = false;

        // raise event
        SetStatus(PlatformStatus.Stopping);

        // stop watchdog
        if (PlatformWatchdog is not null)
            PlatformWatchdog.Stop();

        if (kill)
            KillProcess();

        return true;
    }

    protected virtual void Process_Exited(object? sender, EventArgs e)
    {
        LogManager.LogDebug("{0} has exited", GetType());

        if (KeepAlive)
        {
            if (Tentative < MaxTentative)
            {
                StartProcess();
            }
            else
            {
                LogManager.LogError("Something wen't wrong while trying to start {0}", GetType());
                Stop();

                // reset tentative counter
                Tentative = 0;

                // raise event
                SetStatus(PlatformStatus.Stalled);
            }
        }
    }

    public virtual bool StartProcess()
    {
        try
        {
            // set lock
            IsStarting = true;

            Process process = null;
            while (process is null && Tentative < MaxTentative)
            {
                // increase tentative counter
                Tentative++;

                LogManager.LogDebug("Starting {0}, tentative: {1}/{2}", GetType(), Tentative, MaxTentative);

                // raise event
                SetStatus(PlatformStatus.Starting);

                process = Process.Start(new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                Thread.Sleep(500);
            }

            if (process is not null && !process.HasExited)
            {
                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;

                process.WaitForInputIdle();

                // (re)start watchdog
                PlatformWatchdog.Start();

                // release lock
                IsStarting = false;

                LogManager.LogDebug("{0} has started", GetType());

                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public virtual bool StopProcess()
    {
        return false;
    }

    public bool KillProcess()
    {
        if (Process is null)
            return false;

        try
        {
            Process.Kill();

            // raise event
            SetStatus(PlatformStatus.Stopped);

            return true;
        }
        catch
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

            var diskContent = File.ReadAllBytes(configPath);
            return content.SequenceEqual(diskContent);
        }
        catch (DirectoryNotFoundException)
        {
            // Steam was installed, but got removed
            return false;
        }
        catch (IOException e)
        {
            LogManager.LogError("Couldn't locate {0} configuration file", PlatformType);
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

            File.Move(origPath, configPath, true);
            return true;
        }
        catch (FileNotFoundException e)
        {
            // File was not found (which is valid as it might be before first start of the application)
            LogManager.LogError("Couldn't locate {0} configuration file", PlatformType);
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
        catch (SecurityException)
        {
            return false;
        }
        catch (IOException e)
        {
            LogManager.LogError("Failed to overwrite {0} configuration file", PlatformType);
            return false;
        }
    }

    public bool OverwriteFile(string FilePath, byte[] content, bool backup)
    {
        try
        {
            var configPath = Path.Combine(InstallPath, FilePath);
            if (!FileUtils.IsFileWritable(configPath))
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
        catch (SecurityException)
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
            LogManager.LogError("Failed to overwrite {0} configuration file", PlatformType);
            return false;
        }
    }

    protected void SystemWatcher_Changed(string name, object value)
    {
        SettingValueChanged?.Invoke(name, Convert.ToString(value));
    }

    #region events

    public event StartedEventHandler Updated;
    public delegate void StartedEventHandler(PlatformStatus status);

    public event SettingValueChangedEventHandler SettingValueChanged;
    public delegate void SettingValueChangedEventHandler(string name, object value);

    #endregion
}