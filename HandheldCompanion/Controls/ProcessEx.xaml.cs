using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ControllerCommon;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using static HandheldCompanion.Managers.EnergyManager;

namespace HandheldCompanion.Controls;

/// <summary>
///     Logique d'interaction pour ProcessEx.xaml
/// </summary>
public partial class ProcessEx : UserControl, IDisposable
{
    public delegate void ChildProcessCreatedEventHandler(ProcessEx parent, int Id);

    public delegate void MainThreadChangedEventHandler(ProcessEx process);

    public delegate void TitleChangedEventHandler(ProcessEx process);

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

    public ConcurrentList<int> Children = new();
    private EfficiencyMode EfficiencyMode;

    public ProcessFilter Filter;

    public ImageSource imgSource;

    public ProcessThread MainThread;

    public IntPtr MainWindowHandle;

    public string Path;

    private ThreadState prevThreadState = ThreadState.Terminated;
    private ThreadWaitReason prevThreadWaitReason = ThreadWaitReason.UserRequest;

    public Process Process;
    private readonly int ProcessId;

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
        Title = executable; // temporary, will be overwritten by ProcessManager

        Filter = filter;
        Platform = PlatformManager.GetPlatform(Process);

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

    public string Title
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

    public PlatformType Platform { get; set; }

    public void Dispose()
    {
        if (Process is not null)
            Process.Dispose();
        if (MainThread is not null)
            MainThread.Dispose();
        Children.Dispose();

        GC.SuppressFinalize(this); //now, the finalizer won't be called
    }

    public event MainThreadChangedEventHandler MainThreadChanged;

    public event TitleChangedEventHandler TitleChanged;

    public event ChildProcessCreatedEventHandler ChildProcessCreated;

    public int GetProcessId()
    {
        return ProcessId;
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
                if (thread.ThreadState != ThreadState.Running)
                    continue;

                if (thread.StartTime < startTime)
                {
                    startTime = thread.StartTime;
                    mainThread = thread;
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
            // thread has exited
        }

        return mainThread;
    }

    public bool HasExited()
    {
        if (Process is not null)
            return Process.HasExited;

        return true;
    }

    public void Refresh()
    {
        if (Process.HasExited)
            return;

        if (MainThread is null)
        {
            // refresh main thread
            MainThread = GetMainThread(Process);

            // raise event
            MainThreadChanged?.Invoke(this);

            // prevents null mainthread from passing
            return;
        }

        var MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // refresh title
            if (!string.IsNullOrEmpty(MainWindowTitle) && !MainWindowTitle.Equals(Title))
            {
                Title = MainWindowTitle;

                // raise event
                TitleChanged?.Invoke(this);
            }

            switch (EfficiencyMode)
            {
                default:
                case EfficiencyMode.Default:
                    QoSCheckBox.IsChecked = false;
                    break;

                case EfficiencyMode.Eco:
                    QoSCheckBox.IsChecked = true;
                    break;
            }

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
        });
    }

    public bool IsSuspended()
    {
        return prevThreadWaitReason == ThreadWaitReason.Suspended;
    }

    public EfficiencyMode GetEfficiencyMode()
    {
        return EfficiencyMode;
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
        {
            Children.Add(pid);
            ChildProcessCreated?.Invoke(this, pid);
        }
    }

    public void SetEfficiencyMode(EfficiencyMode mode)
    {
        EfficiencyMode = mode;
    }

    private void SuspendToggle_Toggled(object sender, RoutedEventArgs e)
    {
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
}