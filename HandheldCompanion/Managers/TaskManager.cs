using HandheldCompanion.Views;
using Microsoft.Win32.TaskScheduler;
using System;

namespace HandheldCompanion.Managers
{
    public class TaskManager
    {
        // TaskManager vars
        private Task task;
        private string ServiceName, ServiceExecutable;

        public TaskManager(string ServiceName, string ServiceExecutable)
        {
            this.ServiceName = ServiceName;
            this.ServiceExecutable = ServiceExecutable;

            if (!MainWindow.IsElevated)
                return;

            TaskService TaskServ = new TaskService();
            task = TaskServ.FindTask(ServiceName);

            if (task != null)
                return;

            TaskDefinition td = TaskService.Instance.NewTask();
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            td.Settings.Enabled = false;
            td.Triggers.Add(new LogonTrigger());
            td.Actions.Add(new ExecAction(ServiceExecutable));
            task = TaskService.Instance.RootFolder.RegisterTaskDefinition(ServiceName, td);
        }

        public void UpdateTask(bool value)
        {
            if (task == null)
                return;

            try
            {
                task.Enabled = value;
            }
            catch (Exception) { }
        }
    }
}
