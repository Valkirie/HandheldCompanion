using ControllerHelperWPF.Views;
using Microsoft.Win32.TaskScheduler;
using System;
using Task = Microsoft.Win32.TaskScheduler.Task;

namespace ControllerHelperWPF
{
    public class TaskManager
    {
        // TaskManager vars
        private Task CurrentTask;
        private string ServiceName, ServiceExecutable;

        public TaskManager(string ServiceName, string ServiceExecutable)
        {
            this.ServiceName = ServiceName;
            this.ServiceExecutable = ServiceExecutable;

            if (!MainWindow.IsElevated)
                return;

            TaskService TaskServ = new TaskService();
            CurrentTask = TaskServ.FindTask(ServiceName);

            TaskDefinition td = TaskService.Instance.NewTask();
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            td.Settings.Enabled = false;
            td.Triggers.Add(new LogonTrigger());
            td.Actions.Add(new ExecAction(ServiceExecutable));
            CurrentTask = TaskService.Instance.RootFolder.RegisterTaskDefinition(ServiceName, td);
        }

        public void UpdateTask(bool value)
        {
            if (CurrentTask == null)
                return;

            CurrentTask.Enabled = value;
        }
    }
}
