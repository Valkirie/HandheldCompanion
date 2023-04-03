using ControllerCommon;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using static HandheldCompanion.Managers.EnergyManager;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Logique d'interaction pour ProcessEx.xaml
    /// </summary>
    public partial class ProcessEx : UserControl, IDisposable
    {
        public enum ProcessFilter
        {
            Allowed = 0,
            Restricted = 1,
            Ignored = 2,
        }

        public Process Process;
        public ProcessThread MainThread;

        public ConcurrentList<int> Children = new();

        public IntPtr MainWindowHandle;
        private EfficiencyMode EfficiencyMode;

        public string Path;

        private string _Title;
        public string Title
        {
            get
            {
                return _Title;
            }

            set
            {
                _Title = value;
                TitleTextBlock.Text = value;
            }
        }

        public string _Executable;
        public string Executable
        {
            get
            {
                return _Executable;
            }

            set
            {
                _Executable = value;
                ExecutableTextBlock.Text = value;
            }
        }

        public ProcessFilter Filter;
        public PlatformType Platform { get; set; }

        private ThreadState prevThreadState = ThreadState.Terminated;
        private ThreadWaitReason prevThreadWaitReason = ThreadWaitReason.UserRequest;

        public event ChildProcessCreatedEventHandler ChildProcessCreated;
        public delegate void ChildProcessCreatedEventHandler(ProcessEx parent, int Id);

        public ProcessEx()
        {
            InitializeComponent();
        }

        public ProcessEx(Process process, string path, string executable, ProcessFilter filter) : this()
        {
            this.Process = process;
            this.Path = path;

            this.Executable = executable;
            this.Title = executable;    // temporary, will be overwritten by ProcessManager

            this.Filter = filter;
            this.Platform = PlatformManager.GetPlatform(Process);

            var icon = Icon.ExtractAssociatedIcon(Path);
            if (icon is not null)
                ProcessIcon.Source = icon.ToImageSource();
        }

        public int GetProcessId()
        {
            if (Process is not null)
                return Process.Id;
            return 0;
        }

        private static ProcessThread GetMainThread(Process process)
        {
            ProcessThread mainThread = null;
            var startTime = DateTime.MaxValue;
            foreach (ProcessThread thread in process.Threads)
            {
                try
                {
                    if (thread.StartTime < startTime)
                    {
                        startTime = thread.StartTime;
                        mainThread = thread;
                    }
                }
                catch (InvalidOperationException)
                {
                    // thread has exited
                }
            }
            return mainThread;
        }

        public void Refresh()
        {
            if (Process.HasExited)
                return;

            // pull MainThread if we have none
            if (MainThread is null)
            {
                MainThread = GetMainThread(Process);
                return; // prevents null mainthread from passing
            }

            string MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // refresh title
                if (!string.IsNullOrEmpty(MainWindowTitle))
                    Title = MainWindowTitle;

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
            List<int> childs = ProcessUtils.GetChildIds(Process);

            // remove exited children
            foreach (int pid in childs)
                Children.Remove(pid);

            // raise event on new children
            foreach (int pid in childs)
            {
                Children.Add(pid);
                //ChildProcessCreated?.Invoke(this, child);
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

        public void Dispose()
        {
            if (Process is not null)
                Process.Dispose();
            if (MainThread is not null)
                MainThread.Dispose();
            Children.Dispose();

            GC.SuppressFinalize(this); //now, the finalizer won't be called
        }
    }
}
