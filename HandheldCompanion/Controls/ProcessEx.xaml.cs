using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Foundation.Collections;

namespace HandheldCompanion.Controls;

/// <summary>
///     Logique d'interaction pour ProcessEx.xaml
/// </summary>
public partial class ProcessEx : UserControl, IDisposable
{
    public enum ProcessFilter
    {
        Allowed = 0,
        Restricted = 1,
        Ignored = 2,
        HandheldCompanion = 3,
        Desktop = 4
    }

    public string _Executable;
    private string _Title;
    public string Path;

    public ProcessFilter Filter;
    public PlatformType Platform { get; set; }

    public ImageSource imgSource;

    public ProcessThread MainThread;
    public IntPtr MainWindowHandle;

    private ThreadState prevThreadState = ThreadState.Terminated;
    private ThreadWaitReason prevThreadWaitReason = ThreadWaitReason.UserRequest;

    public Process Process;
    private readonly int ProcessId;
    private LockObject updateLock = new();

    public ConcurrentList<int> Children = new();

    private const string AppCompatRegistry = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
    public static string RunAsAdminRegistryValue = "RUNASADMIN";
    public static string DisabledMaximizedWindowedValue = "DISABLEDXMAXIMIZEDWINDOWEDMODE";
    public static string HighDPIAwareValue = "HIGHDPIAWARE";

    public ProcessEx()
    {
        InitializeComponent();
    }

    public ProcessEx(Process process, string path, string executable, ProcessFilter filter) : this()
    {
        Process = process;
        ProcessId = process.Id;
        Path = path;
        Executable = executable;
        MainWindowTitle = path;
        Filter = filter;

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var icon = Icon.ExtractAssociatedIcon(Path);
            if (icon is not null)
            {
                imgSource = icon.ToImageSource();
                ProcessIcon.Source = imgSource;
            }
        }
    }

    public static string GetAppCompatFlags(string Path)
    {
        if (string.IsNullOrEmpty(Path))
            return string.Empty;

        using (var key = Registry.CurrentUser.OpenSubKey(AppCompatRegistry))
        {
            string valueStr = (string)key?.GetValue(Path);
            return valueStr;
        }
    }

    public static void SetAppCompatFlag(string Path, string Flag, bool value)
    {
        if (string.IsNullOrEmpty(Path))
            return;

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

    public bool FullScreenOptimization
    {
        get
        {
            string valueStr = GetAppCompatFlags(Path);
            return !string.IsNullOrEmpty(valueStr)
                && valueStr.Split(' ').Any(s => s == DisabledMaximizedWindowedValue);
        }

        set
        {
            SetAppCompatFlag(Path, DisabledMaximizedWindowedValue, value);
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

        set
        {
            SetAppCompatFlag(Path, HighDPIAwareValue, value);
        }
    }

    public string MainWindowTitle
    {
        get => _Title;

        set
        {
            _Title = value;
            TitleTextBlock.Text = value;
        }
    }

    public string Executable
    {
        get => _Executable;

        set
        {
            _Executable = value;
            ExecutableTextBlock.Text = value;
        }
    }

    public int GetProcessId()
    {
        return ProcessId;
    }

    public bool HasExited
    {
        get
        {
            if (Process is not null)
                return Process.HasExited;

            return true;
        }
    }

    public bool IsSuspended
    {
        get
        {
            return prevThreadWaitReason == ThreadWaitReason.Suspended;
        }
    }

    public void Refresh()
    {
        if (Process.HasExited)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            using (new ScopedLock(updateLock))
            {
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

                                switch (prevThreadWaitReason)
                                {
                                    case ThreadWaitReason.Suspended:
                                        SuspendToggle.IsOn = true;
                                        break;

                                    default:
                                        SuspendToggle.IsOn = false;
                                        break;
                                }
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

                // update previous state
                prevThreadState = MainThread.ThreadState;

                T_FullScreenOptimization.IsOn = !FullScreenOptimization;
                T_HighDPIAware.IsOn = !HighDPIAware;
            }
        });
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

    private void SuspendToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        switch (SuspendToggle.IsOn)
        {
            case true:
                {
                    if (prevThreadWaitReason == ThreadWaitReason.Suspended)
                        return;

                    ProcessManager.SuspendProcess(this);
                }
                break;
            case false:
                ProcessManager.ResumeProcess(this);
                break;
        }
    }

    private void B_KillProcess_Clicked(object sender, RoutedEventArgs e)
    {
        if (Process is not null)
            Process.Kill();
    }

    public void Dispose()
    {
        if (Process is not null)
            Process.Dispose();
        if (MainThread is not null)
            MainThread.Dispose();
        Children.Dispose();

        GC.SuppressFinalize(this); //now, the finalizer won't be called
    }

    private void FullScreenOptimization_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        FullScreenOptimization = !T_FullScreenOptimization.IsOn;
    }

    private void HighDPIAware_Toggled(object sender, RoutedEventArgs e)
    {
        // wait until lock is released
        if (updateLock)
            return;

        HighDPIAware = !T_HighDPIAware.IsOn;
    }

    internal void MainThreadDisposed()
    {
        MainThread = ProcessManager.GetMainThread(Process);
    }
}