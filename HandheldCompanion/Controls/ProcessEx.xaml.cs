using ControllerCommon.Platforms;
using ControllerCommon.Processor;
using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static HandheldCompanion.Managers.EnergyManager;

namespace HandheldCompanion.Controls
{
    /// <summary>
    /// Logique d'interaction pour ProcessEx.xaml
    /// </summary>
    public partial class ProcessEx : UserControl
    {
        public enum ProcessFilter
        {
            Allowed = 0,
            Restricted = 1,
            Ignored = 2,
        }

        public Process Process;
        public ProcessThread MainThread;

        public List<int> Children = new();

        public IntPtr MainWindowHandle;
        private EfficiencyMode EfficiencyMode;

        public string Path;

        public string Title
        {
            get
            {
                return TitleTextBlock.Text;
            }

            set
            {
                TitleTextBlock.Text = value;
            }
        }

        public string Executable
        {
            get
            {
                return ExecutableTextBlock.Text;
            }

            set
            {
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
            this.Title = executable;

            this.Filter = filter;
            this.Platform = PlatformManager.GetPlatform(Process);

            var icon = Icon.ExtractAssociatedIcon(Path);
            if (icon is not null)
                ProcessIcon.Source = icon.ToImageSource();
        }

        public int GetProcessId()
        {
            return Process.Id;
        }

        private static ProcessThread GetMainThread(Process process)
        {
            ProcessThread mainThread = null;
            var startTime = DateTime.MaxValue;
            foreach (ProcessThread thread in process.Threads)
            {
                if (thread.StartTime < startTime)
                {
                    startTime = thread.StartTime;
                    mainThread = thread;
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
                MainThread = GetMainThread(Process);

            string MainWindowTitle = ProcessUtils.GetWindowTitle(MainWindowHandle);

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // refresh title
                if (!string.IsNullOrEmpty(MainWindowTitle))
                    Title = MainWindowTitle;

                switch (MainThread.ThreadState)
                {
                    case ThreadState.Wait:
                        {
                            // monitor if the process main thread was suspended or resumed
                            if (MainThread.WaitReason != prevThreadWaitReason)
                            {
                                switch (MainThread.WaitReason)
                                {
                                    case ThreadWaitReason.Suspended:
                                        SuspendToggle.IsOn = true;
                                        break;

                                    default:
                                        SuspendToggle.IsOn = false;
                                        break;
                                }
                            }

                            prevThreadWaitReason = MainThread.WaitReason;
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
            });

            // update previous state
            prevThreadState = MainThread.ThreadState;
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
            Children.RemoveAll(item => !childs.Contains(item));

            // raise event on new children
            foreach (int child in childs.Where(item => !Children.Contains(item)))
            {
                Children.Add(child);
                //ChildProcessCreated?.Invoke(this, child);
            }
        }

        public void SetEfficiencyMode(EfficiencyMode mode)
        {
            EfficiencyMode = mode;

            switch(mode)
            {
                default:
                case EfficiencyMode.Default:
                    QoSCheckBox.IsChecked = false;
                    break;

                case EfficiencyMode.Eco:
                    QoSCheckBox.IsChecked = true;
                    break;
            }
        }

        private void SuspendToggle_Toggled(object sender, RoutedEventArgs e)
        {
            switch(SuspendToggle.IsOn)
            {
                case true:
                    ProcessManager.SuspendProcess(this);
                    break;
                case false:
                    ProcessManager.ResumeProcess(this);
                    break;
            }
        }
    }
}
