using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Views;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_LegionGoDaemon : IHint
    {
        private Process process;
        private const string taskName = "LSDaemon";

        public Hint_LegionGoDaemon() : base()
        {
            if (MainWindow.CurrentDevice is not LegionGo)
                return;

            ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
            ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_SteamNeptuneDesktopWarning;
            this.HintDescription.Text = Properties.Resources.Hint_SteamNeptuneDesktopDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_SteamNeptuneDesktopAction;

            this.HintActionButton.Content = $"Disable {taskName}";
        }

        private void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            if (processEx.Executable.Equals(taskName, StringComparison.InvariantCultureIgnoreCase))
                this.Visibility = Visibility.Visible;

            process = processEx.Process;
        }

        private void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            if (processEx.Executable.Equals(taskName, StringComparison.InvariantCultureIgnoreCase))
                this.Visibility = Visibility.Collapsed;

            process = null;
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the task service instance
            using (TaskService ts = new TaskService())
            {
                // Get the task by name
                Task task = ts.GetTask(taskName);
                if (task != null && task.State == TaskState.Running)
                {
                    task.Stop();
                    task.Enabled = false;
                }
            }

            // If the process exists and is running, kill it
            if (process != null && !process.HasExited)
                process.Kill();
        }
    }
}
