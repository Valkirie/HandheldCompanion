using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace HandheldCompanion.Controls;

public class ProcessEx : IDisposable
{
    public enum ProcessFilter
    {
        Allowed = 0,
        Restricted = 1,
        Ignored = 2,
        HandheldCompanion = 3,
        Desktop = 4
    }

    private const string AppCompatRegistry = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    public static string RunAsAdminRegistryValue = "RUNASADMIN";
    public static string DisabledMaximizedWindowedValue = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
    public static string HighDPIAwareValue = "HIGHDPIAWARE";

    public Process Process { get; private set; }
    public int ProcessId { get; private set; }

    public ProcessFilter Filter { get; set; }
    public PlatformType Platform { get; set; }
    public string Path { get; set; }
    public ImageSource ProcessIcon { get; private set; }

    public ProcessThread MainThread { get; set; }

    private IntPtr _MainWindowHandle;
    public IntPtr MainWindowHandle
    {
        get
        {
            return _MainWindowHandle;
        }
        set
        {
            _MainWindowHandle = value;

            string WindowTitle = ProcessUtils.GetWindowTitle(value);
            MainWindowTitle = string.IsNullOrEmpty(WindowTitle) ? Executable : WindowTitle;
        }
    }

    public ConcurrentList<int> Children = new();

    public EventHandler Refreshed;

    private ThreadWaitReason prevThreadWaitReason = ThreadWaitReason.UserRequest;

    public ProcessEx() { }
    public ProcessEx(Process process, string path, string executable, ProcessFilter filter)
    {
        Process = process;
        ProcessId = process.Id;
        Path = path;
        Executable = executable;
        MainWindowTitle = path;
        Filter = filter;

        if (!string.IsNullOrEmpty(Path) && File.Exists(Path))
        {
            var icon = Icon.ExtractAssociatedIcon(Path);
            if (icon is not null)
            {
                ProcessIcon = icon.ToImageSource();
            }
        }
    }

    public static string GetAppCompatFlags(string Path)
    {
        if (string.IsNullOrEmpty(Path))
            return string.Empty;

        lock (registryLock)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(AppCompatRegistry))
                {
                    string valueStr = (string)key?.GetValue(Path);
                    return valueStr;
                }
            }
            catch { }
        }

        return string.Empty;
    }

    private static object registryLock = new();
    public static void SetAppCompatFlag(string Path, string Flag, bool value)
    {
        if (string.IsNullOrEmpty(Path))
            return;

        lock (registryLock)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(AppCompatRegistry, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    if (key != null)
                    {
                        List<string> values = new List<string> { "~" }; ;
                        string valueStr = (string)key.GetValue(Path);

                        if (!string.IsNullOrEmpty(valueStr))
                            values = valueStr.Split(' ').ToList();

                        values.Remove(Flag);

                        if (value)
                            values.Add(Flag);

                            if (values.Count == 1 && values[0] == "~" && !string.IsNullOrEmpty(valueStr))
                                key.DeleteValue(Path);
                            else
                                key.SetValue(Path, string.Join(" ", values), RegistryValueKind.String);
                    }
                }
            }
            catch { }
        }
    }

    public bool FullScreenOptimization
    {
        get
        {
            string valueStr = GetAppCompatFlags(Path);
            return !string.IsNullOrEmpty(valueStr)
                && valueStr.Split(' ').Any(s => s == DisabledMaximizedWindowedValue);
        }
    }

    public bool HighDPIAware
    {
        get
        {
            string valueStr = GetAppCompatFlags(Path);
            return !string.IsNullOrEmpty(valueStr)
                && valueStr.Split(' ').Any(s => s == HighDPIAwareValue);
        }
    }

    public string MainWindowTitle { get; private set; }

    public string Executable { get; set; }

    private bool _isSuspended;
    public bool IsSuspended
    {
        get => _isSuspended;
        set
        {
            if (value)
            {
                if (prevThreadWaitReason == ThreadWaitReason.Suspended)
                    return;

                ProcessManager.SuspendProcess(this);
            }
            else
            {
                ProcessManager.ResumeProcess(this);
            }
            _isSuspended = value;
        }
    }

    public void Refresh()
    {
        if (Process.HasExited)
            return;

        if (MainThread is null)
            return;

        switch (MainThread.ThreadState)
        {
            case ThreadState.Wait:
                {
                    // monitor if the process main thread was suspended or resumed
                    if (MainThread.WaitReason != prevThreadWaitReason)
                    {
                        prevThreadWaitReason = MainThread.WaitReason;
                        _isSuspended = prevThreadWaitReason == ThreadWaitReason.Suspended;
                    }
                }
                break;

            case ThreadState.Terminated:
                {
                    // dispose from MainThread
                    MainThread.Dispose();
                    MainThread = null;
                }
                break;
        }

        // update main window handle
        MainWindowHandle = Process.MainWindowHandle;

        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshChildProcesses()
    {
        // refresh all child processes
        var childs = ProcessUtils.GetChildIds(Process);

        // remove exited children
        foreach (var pid in childs)
            Children.Remove(pid);

        // raise event on new children
        foreach (var pid in childs)
            Children.Add(pid);
    }

    public void Dispose()
    {
        Process?.Dispose();
        MainThread?.Dispose();
        Children.Dispose();

        GC.SuppressFinalize(this); //now, the finalizer won't be called
    }

    internal void MainThreadDisposed()
    {
        MainThread = ProcessManager.GetMainThread(Process);
    }
}