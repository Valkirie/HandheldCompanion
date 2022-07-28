using HandheldCompanion.Views;
using Microsoft.Win32.TaskScheduler;
using System;

namespace HandheldCompanion.Managers
{
    public class TaskManager
    {
        // TaskManager vars
        private Task task;
        private TaskDefinition taskDefinition;
        private string ServiceName, ServiceExecutable;

        public TaskManager(string ServiceName, string Executable)
        {
            this.ServiceName = ServiceName;
            this.ServiceExecutable = Executable;

            if (!MainWindow.IsElevated)
                return;

            TaskService TaskServ = new TaskService();
            task = TaskServ.FindTask(ServiceName);

            if (task != null)
            {
                task.Definition.Actions.Clear();
                task.Definition.Actions.Add(new ExecAction(Executable));
                task = TaskService.Instance.RootFolder.RegisterTaskDefinition(ServiceName, task.Definition);
            }
            else
            {
                taskDefinition = TaskService.Instance.NewTask();
                taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
                taskDefinition.Settings.DisallowStartIfOnBatteries = false;
                taskDefinition.Settings.StopIfGoingOnBatteries = false;
                taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                taskDefinition.Settings.Enabled = false;
                taskDefinition.Triggers.Add(new LogonTrigger());
                taskDefinition.Actions.Add(new ExecAction(Executable));
                task = TaskService.Instance.RootFolder.RegisterTaskDefinition(ServiceName, taskDefinition);
            }
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
