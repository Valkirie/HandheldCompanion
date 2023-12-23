using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

    public ConcurrentList<int> Children = new();

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
        MainWindowTitle = executable; // temporary, will be overwritten by ProcessManager

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

    public int GetProcessId()
    {
        return ProcessId;
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

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
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

        Refresh();
    }

    private void B_KillProcess_Clicked(object sender, RoutedEventArgs e)
    {
        if (Process is not null)
            Process.Kill();
    }
}