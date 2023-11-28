using HandheldCompanion.Devices;
using HandheldCompanion.Views;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_LegionGoDaemon : IHint
    {
        private Process process;
        private const string taskName = "LSDaemon";
        private Timer taskTimer;

        public Hint_LegionGoDaemon() : base()
        {
            if (MainWindow.CurrentDevice is not LegionGo)
                return;

            taskTimer = new Timer(4000);
            taskTimer.Elapsed += TaskTimer_Elapsed;
            taskTimer.Start();

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_LegionGoDaemon;
            this.HintDescription.Text = Properties.Resources.Hint_LegionGoDaemonDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_LegionGoDaemonReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_LegionGoDaemonAction;
        }

        private void TaskTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Get all the processes with the given name
            process = Process.GetProcessesByName(taskName).FirstOrDefault();

            // If there is at least one process, return true
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (process is not null && !process.HasExited)
                    this.Visibility = Visibility.Visible;
                else
                    this.Visibility = Visibility.Collapsed;
            });
        }

        protected override void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                // Get the task service instance
                using (TaskService ts = new TaskService())
                {
                    // Get the task by name
                    Microsoft.Win32.TaskScheduler.Task task = ts.GetTask(taskName);
                    if (task != null && task.State == TaskState.Running)
                    {
                        task.Stop();
                        task.Enabled = false;
                    }
                }
            });

            // If the process exists and is running, kill it
            if (process != null && !process.HasExited)
                process.Kill();
        }
    }
}
