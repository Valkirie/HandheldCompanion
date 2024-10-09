using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Media;

namespace HandheldCompanion.Controls;

public class ProcessWindow
{
    public AutomationElement Element;
    public readonly int Hwnd;
    public string Name;

    public EventHandler Refreshed;

    public ProcessWindow(AutomationElement element, bool isPrimary)
    {
        this.Hwnd = element.Current.NativeWindowHandle;
        Refresh();
    }

    public void Refresh()
    {
        try
        {
            Element = AutomationElement.FromHandle(this.Hwnd);

            if (!string.IsNullOrEmpty(Element.Current.Name))
                Name = Element.Current.Name;
            else
            {
                // backup method
                string title = ProcessUtils.GetWindowTitle(Hwnd);
                if (string.IsNullOrEmpty(title))
                    return;

                Name = title;
            }

            Refreshed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
        }
    }
}

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

    public const string AppCompatRegistry = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    public const string RunAsAdminRegistryValue = "RUNASADMIN";
    public const string DisabledMaximizedWindowedValue = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
    public const string HighDPIAwareValue = "HIGHDPIAWARE";

    public Process Process { get; private set; }
    public int ProcessId { get; private set; }

    public ProcessFilter Filter { get; set; }
    public PlatformType Platform { get; set; }
    public string Path { get; set; }
    public ImageSource ProcessIcon { get; private set; }

    public ConcurrentDictionary<int, ProcessWindow> ProcessWindows { get; private set; } = new();
    public ConcurrentList<int> ChildrenProcessIds = [];

    private ProcessThread MainThread { get; set; }
    private ProcessThread _previousMainThread;
    private ThreadWaitReason prevThreadWaitReason = ThreadWaitReason.UserRequest;

    private static object registryLock = new();

    #region event
    public EventHandler Refreshed;

    public event WindowAttachedEventHandler WindowAttached;
    public delegate void WindowAttachedEventHandler(ProcessWindow processWindow);

    public event WindowDetachedEventHandler WindowDetached;
    public delegate void WindowDetachedEventHandler(ProcessWindow processWindow);
    #endregion

    public ProcessEx(Process process, string path, string executable, ProcessFilter filter)
    {
        Process = process;
        ProcessId = process.Id;
        Path = path;
        Executable = executable;
        Filter = filter;

        // get main thread
        MainThread = GetMainThread(process);

        // update main thread when disposed
        SubscribeToDisposedEvent(MainThread);

        // get executable icon
        if (File.Exists(Path))
        {
            Icon? icon = Icon.ExtractAssociatedIcon(Path);
            ProcessIcon = icon?.ToImageSource();
        }
    }

    public void AttachWindow(AutomationElement automationElement, bool primary = false)
    {
        int hwnd = automationElement.Current.NativeWindowHandle;

        if (!ProcessWindows.TryGetValue(hwnd, out var window))
        {
            // create new window object
            window = new(automationElement, primary);

            // update window
            ProcessWindows[hwnd] = window;
        }

        if (string.IsNullOrEmpty(window.Name))
            return;

        // raise event
        WindowAttached?.Invoke(window);
    }

    public void DetachWindow(int hwnd)
    {
        // raise event
        if (ProcessWindows.TryRemove(hwnd, out ProcessWindow processWindow))
            WindowDetached?.Invoke(processWindow);
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

        // refresh attached windows
        foreach (ProcessWindow processWindow in ProcessWindows.Values)
            processWindow.Refresh();

        // raise event
        Refreshed?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshChildProcesses()
    {
        // refresh all child processes
        var childs = ProcessUtils.GetChildIds(Process);

        // remove exited children
        foreach (var pid in childs)
            ChildrenProcessIds.Remove(pid);

        // raise event on new children
        foreach (var pid in childs)
            ChildrenProcessIds.Add(pid);
    }

    private void SubscribeToDisposedEvent(ProcessThread newMainThread)
    {
        if (newMainThread is null)
            return;

        // Unsubscribe from the previous MainThread's Disposed event
        if (_previousMainThread != null)
        {
            _previousMainThread.Disposed -= MainThread_Disposed;
        }

        // Subscribe to the new MainThread's Disposed event
        newMainThread.Disposed += MainThread_Disposed;

        // Update the previous MainThread reference
        _previousMainThread = newMainThread;
    }

    private void MainThread_Disposed(object sender, EventArgs e)
    {
        // Update MainThread when disposed
        MainThread = GetMainThread(Process);
        // Subscribe to the new MainThread's Disposed event
        SubscribeToDisposedEvent(MainThread);
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
                        List<string> values = ["~"]; ;
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

    private static ProcessThread GetMainThread(Process process)
    {
        ProcessThread mainThread = null;
        var startTime = DateTime.MaxValue;

        try
        {
            if (process.Threads is null || process.Threads.Count == 0)
                return null;

            foreach (ProcessThread thread in process.Threads)
            {
                try
                {
                    if (thread.ThreadState != ThreadState.Running)
                        continue;

                    if (thread.StartTime < startTime)
                    {
                        startTime = thread.StartTime;
                        mainThread = thread;
                    }
                }
                catch (InvalidOperationException)
                {
                    // This exception occurs if the thread has exited
                }
                catch (Exception)
                {
                    // Handle other exceptions
                }
            }

            if (mainThread is null)
                mainThread = process.Threads[0];
        }
        catch (Win32Exception)
        {
            // Access if denied
        }
        catch (InvalidOperationException)
        {
            // This exception occurs if the thread has exited
        }

        return mainThread;
    }

    public void Dispose()
    {
        Process?.Dispose();
        MainThread?.Dispose();
        ChildrenProcessIds.Dispose();
        ProcessWindows.Clear();

        GC.SuppressFinalize(this); //now, the finalizer won't be called
    }
}