using HandheldCompanion.Devices;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using Task = System.Threading.Tasks.Task;
using TaskScheduled = Microsoft.Win32.TaskScheduler.Task;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_MSIClawCenter : IHint
    {
        private List<string> serviceNames = new()
        {
            "ArmouryCrateSEService",
            "AsusAppService",
            "ArmouryCrateControlInterface",
        };

        private Timer watcherTimer;
        private const string TaskName = "MSI_Center_M_Server";
        private const string UIname = "MSI Center M";

        public Hint_MSIClawCenter() : base()
        {
            if (IDevice.GetCurrent() is not ClawA1M)
                return;

            // we'll be checking MSI Claw Task every 4 seconds
            watcherTimer = new Timer(4000);
            watcherTimer.Elapsed += WatcherTimer_Elapsed;
            watcherTimer.Start();

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_MSIClawCenterCheck;
            this.HintDescription.Text = Properties.Resources.Hint_MSIClawCenterCheckDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_MSIClawCenterCheckReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_MSIClawCenterCheckAction;
        }

        private TaskScheduled GetTask()
        {
            using (TaskService taskService = new TaskService())
            {
                TaskScheduled task = taskService.GetTask(TaskName);
                return task;
            }
        }

        private bool HasTask()
        {
            TaskScheduled task = GetTask();
            if (task is null)
                return false;
            
            return task.Enabled;
        }

        private void KillTask()
        {
            TaskScheduled task = GetTask();
            if (task != null)
                task.Enabled = false;
        }

        private IEnumerable<Process> GetProcesses()
        {
            Process[] processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                if (process.ProcessName.StartsWith(TaskName, StringComparison.InvariantCultureIgnoreCase))
                    yield return process;

                if (process.ProcessName.Contains(UIname, StringComparison.InvariantCultureIgnoreCase))
                    yield return process;
            }
        }

        private bool HasProcesses()
        {
            return GetProcesses().Any();
        }

        private void KillProcesses()
        {
            foreach (Process process in GetProcesses())
                process.Kill();
        }

        private void WatcherTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            bool hasTask = HasTask();
            bool hasProcesses = HasProcesses();

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.Visibility = hasTask || hasProcesses ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            KillTask();
            KillProcesses();
        }

        public override void Stop()
        {
            watcherTimer.Stop();
            base.Stop();
        }
    }
}
